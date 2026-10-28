using System;
using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets.Contracts;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.FieldTurrets
{
    /// <summary>씬의 전력 예산을 소유하고 ID 기반 터렛 활성화 요청을 처리한다.</summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TeamHJD.Game.Turrets", sourceAssembly: "TeamHJD.Game.Turrets", sourceClassName: "TurretController")]
    public sealed class FieldTurretController : MonoBehaviour, IFieldTurretPowerSource
    {
        [SerializeField, Min(0)] private int maximumPower = 250;
        [Header("Unlock Grid")]
        [SerializeField] private Vector2 unlockGridOrigin;
        [SerializeField] private float unlockGridOriginZ;
        [SerializeField, Min(0.1f)] private float unlockGridMapWidth = FieldTurretGridConfiguration.DefaultMapWidth;
        [SerializeField, Min(0.1f)] private float unlockGridMapHeight = FieldTurretGridConfiguration.DefaultMapHeight;
        [SerializeField, Min(1)] private int unlockGridCellsX = FieldTurretGridConfiguration.DefaultCellsX;
        [SerializeField, Min(1)] private int unlockGridCellsY = FieldTurretGridConfiguration.DefaultCellsY;
        [SerializeField, Min(1)] private int unlockZoneCount = 4;
        private FieldTurretUnlockZones _unlockZones;
        private IRegionAccessPolicy _regionAccess;
        private int _unlockedZoneCount;
        public FieldTurretUnlockZones UnlockZones => _unlockZones;
        public int UnlockedZoneCount => _unlockedZoneCount;
        private FieldTurretPowerBudget _budget;
        public FieldTurretPowerBudget Budget => _budget ??= new FieldTurretPowerBudget(Mathf.Max(0, maximumPower));
        public int CurrentPower => Budget.CurrentPower;
        public int MaximumPower => Budget.MaximumPower;
        public IRegionGridSource Regions => _regionAccess?.Regions;
        public event Action<int, int> PowerChanged
        {
            add => Budget.PowerChanged += value;
            remove => Budget.PowerChanged -= value;
        }

        private void Awake() { _ = Budget; }
        private void OnEnable()
        {
            FieldTurretInstanceRegistry.Registered += ApplyUnlockZoneToNewTurret;
            if (_regionAccess != null)
            {
                _regionAccess.Regions.Changed += OnRegionChanged;
                ApplyRegionRestrictions();
            }
        }
        private void Update()
        {
            Budget.Tick(Time.deltaTime);
            if (IsAutoActivationRunning) AdvanceAutoActivation();
        }

        public const float AutoActivationCooldownSeconds = 60f;
        private float _nextAutoActivationTime;
        private readonly List<AutoActivationCandidate> _autoCandidates = new();
        private IReadOnlyList<AutoActivationCandidate> _autoCandidatesView;
        public bool IsAutoActivationRunning { get; private set; }
        public float AutoActivationCooldownRemaining => Mathf.Max(0f, _nextAutoActivationTime - Time.time);
        public IReadOnlyList<AutoActivationCandidate> AutoActivationCandidates =>
            _autoCandidatesView ??= _autoCandidates.AsReadOnly();

        // Optional future scoring policy. Evaluated only while capturing the button-time list.
        public Func<FieldTurretBase, int, float> AutoActivationScoreProvider { get; set; }

        public sealed class AutoActivationCandidate
        {
            public int InstanceId { get; }
            public int EnemyCount { get; }
            public float Score { get; }
            internal bool Completed;
            internal AutoActivationCandidate(int id, int enemyCount, float score)
            {
                InstanceId = id;
                EnemyCount = enemyCount;
                Score = score;
            }
        }

        /// <summary>CU interaction/button calls this once. No CU component is required.</summary>
        public bool TryStartAutoActivation()
        {
            if (!isActiveAndEnabled || IsAutoActivationRunning || AutoActivationCooldownRemaining > 0f)
                return false;

            var owned = new List<FieldTurretBase>();
            _autoCandidates.Clear();
            foreach (FieldTurretBase turret in FieldTurretInstanceRegistry.RegisteredInstances.Values)
            {
                if (turret == null || !turret.isActiveAndEnabled ||
                    turret.gameObject.scene != gameObject.scene || !turret.UsesPowerSource(this)) continue;
                owned.Add(turret);
                if (turret.IsLocked || turret.IsDestroyed) continue;
                int count = turret.CountEnemiesInAttackRange();
                float score = AutoActivationScoreProvider != null ? AutoActivationScoreProvider(turret, count) : count;
                if (float.IsNaN(score) || float.IsInfinity(score)) score = count;
                _autoCandidates.Add(new AutoActivationCandidate(turret.InstanceId, count, score));
            }
            _autoCandidates.Sort((left, right) =>
            {
                int byScore = right.Score.CompareTo(left.Score);
                return byScore != 0 ? byScore : left.InstanceId.CompareTo(right.InstanceId);
            });

            _nextAutoActivationTime = Time.time + AutoActivationCooldownSeconds;
            IsAutoActivationRunning = true;
            foreach (FieldTurretBase turret in owned)
                if (turret.IsActivated) turret.RequestSystemActivation(false);
            AdvanceAutoActivation();
            return true;
        }

        private void AdvanceAutoActivation()
        {
            bool hasCoolingCandidate = false;
            bool hasPowerPendingCandidate = false;
            foreach (AutoActivationCandidate candidate in _autoCandidates)
            {
                if (candidate.Completed) continue;
                if (!FieldTurretInstanceRegistry.TryGet(candidate.InstanceId, out FieldTurretBase turret) || turret == null ||
                    !turret.isActiveAndEnabled || turret.gameObject.scene != gameObject.scene ||
                    !turret.UsesPowerSource(this) || turret.IsLocked || turret.IsDestroyed)
                {
                    candidate.Completed = true;
                    continue;
                }
                // Manual activation satisfies this candidate. Never activate it twice after a manual off.
                if (turret.IsActivated)
                {
                    candidate.Completed = true;
                    continue;
                }
                FieldTurretActivationResult result = turret.RequestSystemActivation(true);
                if (result == FieldTurretActivationResult.CoolingDown)
                {
                    hasCoolingCandidate = true;
                    continue;
                }

                candidate.Completed = result != FieldTurretActivationResult.InsufficientPower;
                if (!candidate.Completed) hasPowerPendingCandidate = true;
            }
            if (!hasCoolingCandidate &&
                (!hasPowerPendingCandidate || Budget.PendingRecovery == 0))
                IsAutoActivationRunning = false;
        }

        private void OnDisable()
        {
            FieldTurretInstanceRegistry.Registered -= ApplyUnlockZoneToNewTurret;
            if (_regionAccess != null) _regionAccess.Regions.Changed -= OnRegionChanged;
            IsAutoActivationRunning = false;
        }

        public bool TrySetMaximumPower(int power)
        {
            if (!Budget.TrySetMaximumPower(power)) return false;
            maximumPower = power;
            return true;
        }

        public bool TryConsumePower(int power) => Budget.TryConsumePower(power);
        public bool TryChangeReservation(int previousPower, int newPower) => Budget.TryChangeReservation(previousPower, newPower);
        public void ReleasePower(int power) => Budget.ReleasePower(power);

        /// <summary>메인 스레드에서 호출한다. 같은 전력 공급자를 사용하는 등록 터렛만 집계한다.</summary>
        public FieldTurretControllerSnapshot GetSnapshot()
        {
            int registered = 0;
            int activated = 0;
            int operational = 0;
            var activatedByType = new Dictionary<FieldTurretKind, int>();
            foreach (FieldTurretKind kind in Enum.GetValues(typeof(FieldTurretKind)))
                activatedByType[kind] = 0;

            foreach (FieldTurretBase turret in FieldTurretInstanceRegistry.RegisteredInstances.Values)
            {
                if (turret == null || !turret.UsesPowerSource(this)) continue;
                registered++;
                if (turret.IsOperational) operational++;
                if (!turret.IsActivated || turret.IsDestroyed) continue;
                activated++;
                activatedByType.TryGetValue(turret.Kind, out int count);
                activatedByType[turret.Kind] = count + 1;
            }

            FieldTurretPowerBudget budget = Budget;
            return new FieldTurretControllerSnapshot(
                budget.MaximumPower, budget.CurrentPower, budget.ReservedPower, budget.PendingRecovery,
                registered, activated, operational, activatedByType);
        }

        public FieldTurretActivationResult RequestActivation(int instanceId, bool shouldActivate)
        {
            if (!FieldTurretInstanceRegistry.TryGet(instanceId, out FieldTurretBase turret) || turret == null ||
                !turret.UsesPowerSource(this))
                return FieldTurretActivationResult.PowerSourceUnavailable;
            return turret.RequestActivation(shouldActivate);
        }

        /// <summary>Builds the turret-owned grid from Inspector settings. Zone one starts unlocked.</summary>
        public bool ConfigureUnlockZones(Vector2 cuPosition)
            => ConfigureUnlockZones(cuPosition, unlockZoneCount);

        public bool ConfigureUnlockZones(Vector2 cuPosition, int zoneCount)
        {
            FieldTurretGridConfiguration grid = CreateUnlockGridConfiguration();
            return ConfigureUnlockZones(grid, cuPosition, zoneCount);
        }

        /// <summary>Configures cell-distance bands. Zone one is available from the start.</summary>
        public bool ConfigureUnlockZones(FieldTurretGridConfiguration grid, Vector2 cuPosition, int zoneCount = 4)
        {
            if (_regionAccess != null)
                throw new InvalidOperationException("The Match region policy owns access; update it through Core.");
            FieldTurretUnlockZones zones = new FieldTurretUnlockZones(grid, cuPosition, zoneCount);
            _unlockZones = zones;
            _unlockedZoneCount = 1;
            bool applied = true;
            foreach (FieldTurretBase turret in FieldTurretInstanceRegistry.RegisteredInstances.Values)
            {
                if (turret == null || turret.gameObject.scene != gameObject.scene ||
                    !turret.UsesPowerSource(this)) continue;
                applied &= ApplyUnlockZone(turret);
            }
            return applied;
        }

        /// <summary>Unlocks the requested zone and all nearer zones. A game rule may call this later.</summary>
        public bool UnlockThroughZone(int zone)
        {
            if (_regionAccess != null) return false;
            if (_unlockZones == null || zone < _unlockedZoneCount || zone > _unlockZones.ZoneCount)
                return false;
            _unlockedZoneCount = zone;
            bool applied = true;
            foreach (FieldTurretBase turret in FieldTurretInstanceRegistry.RegisteredInstances.Values)
            {
                if (turret == null || turret.gameObject.scene != gameObject.scene ||
                    !turret.UsesPowerSource(this)) continue;
                applied &= ApplyUnlockZone(turret);
            }
            return applied;
        }

        public bool TryGetUnlockZone(FieldTurretBase turret, out int zone)
        {
            zone = 0;
            return _unlockZones != null && turret != null &&
                   turret.gameObject.scene == gameObject.scene && turret.UsesPowerSource(this) &&
                   _unlockZones.TryGetZone(turret.transform.position, out zone);
        }

        internal bool CanUnlock(FieldTurretBase turret)
        {
            if (_regionAccess != null)
            {
                Vector3 position = turret.transform.position;
                return _regionAccess.CanUseAt(new BattlefieldPoint(position.x, position.y));
            }
            return _unlockZones == null ||
                TryGetUnlockZone(turret, out int zone) && zone <= _unlockedZoneCount;
        }

        /// <summary>Inspector 설정의 정적 구역 정의만 계산합니다. 터렛 상태나 해금을 변경하지 않습니다.</summary>
        public FieldTurretUnlockZones CreateUnlockZoneDefinition(Vector2 cuPosition)
        {
            return new FieldTurretUnlockZones(CreateUnlockGridConfiguration(), cuPosition, unlockZoneCount);
        }

        /// <summary>CU 거리 정책과 별개로 동일한 지역 Grid 공간 정의를 읽습니다.</summary>
        public FieldTurretGridConfiguration CreateUnlockGridConfiguration() => new FieldTurretGridConfiguration(
            unlockGridOrigin, unlockGridMapWidth, unlockGridMapHeight, unlockGridCellsX, unlockGridCellsY, unlockGridOriginZ);

        /// <summary>Core의 읽기 정책을 연결합니다. null 해제는 기존 터렛 상태를 다시 쓰지 않습니다.</summary>
        public void SetRegionAccessPolicy(IRegionAccessPolicy policy)
        {
            if (ReferenceEquals(_regionAccess, policy)) return;
            if (_regionAccess != null) _regionAccess.Regions.Changed -= OnRegionChanged;
            _regionAccess = policy;
            if (_regionAccess == null) return;
            if (isActiveAndEnabled) _regionAccess.Regions.Changed += OnRegionChanged;
            ApplyRegionRestrictions();
        }

        private void OnRegionChanged(RegionGridSnapshot snapshot) => ApplyRegionRestrictions();

        private void ApplyRegionRestrictions()
        {
            foreach (FieldTurretBase turret in new List<FieldTurretBase>(FieldTurretInstanceRegistry.RegisteredInstances.Values))
            {
                if (_regionAccess == null) break;
                if (turret != null && turret.gameObject.scene == gameObject.scene && turret.UsesPowerSource(this))
                    ApplyUnlockZone(turret);
            }
        }

        private void ApplyUnlockZoneToNewTurret(FieldTurretBase turret)
        {
            if ((_unlockZones != null || _regionAccess != null) && turret != null && turret.gameObject.scene == gameObject.scene &&
                turret.UsesPowerSource(this))
                ApplyUnlockZone(turret);
        }

        private bool ApplyUnlockZone(FieldTurretBase turret)
        {
            bool shouldLock = !CanUnlock(turret);
            if (turret.IsLocked == shouldLock) return true;
            return turret.ApplyZoneLocked(shouldLock);
        }

        public static FieldTurretController FindForScene(Scene scene)
        {
            foreach (FieldTurretController controller in FindObjectsByType<FieldTurretController>(FindObjectsSortMode.None))
                if (controller.gameObject.scene == scene && controller.isActiveAndEnabled) return controller;
            return null;
        }

        // Compatibility bootstrap: no CU or specially named object is required.
        public static FieldTurretController GetOrCreateForScene(Scene scene)
        {
            FieldTurretController controller = FindForScene(scene);
            if (controller != null) return controller;
            GameObject owner = new GameObject("FieldTurretController");
            SceneManager.MoveGameObjectToScene(owner, scene);
            return owner.AddComponent<FieldTurretController>();
        }
    }
}

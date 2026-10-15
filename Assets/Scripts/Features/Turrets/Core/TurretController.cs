// Scene의 터렛 전력·자동 활성화와 지역 사용 제한을 관리하는 D Feature 소유자입니다.
// Core 조립부에는 실제 소유 개체를 확인하는 읽기 API를 제공합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Turrets.Contracts;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Turrets
{
    /// <summary>씬의 전력 예산을 소유하고 ID 기반 터렛 활성화 요청을 처리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class TurretController : MonoBehaviour, ITurretPowerSource
    {
        [SerializeField, Min(0)] private int maximumPower = 250;
        [Header("Unlock Grid")]
        [SerializeField] private Vector2 unlockGridOrigin;
        [SerializeField] private float unlockGridOriginZ;
        [SerializeField, Min(0.1f)] private float unlockGridMapWidth = TurretGridConfiguration.DefaultMapWidth;
        [SerializeField, Min(0.1f)] private float unlockGridMapHeight = TurretGridConfiguration.DefaultMapHeight;
        [SerializeField, Min(1)] private int unlockGridCellsX = TurretGridConfiguration.DefaultCellsX;
        [SerializeField, Min(1)] private int unlockGridCellsY = TurretGridConfiguration.DefaultCellsY;
        [SerializeField, Min(1)] private int unlockZoneCount = 4;
        private TurretUnlockZones _unlockZones;
        private IRegionAccessPolicy _regionAccess;
        private int _unlockedZoneCount;
        public TurretUnlockZones UnlockZones => _unlockZones;
        public int UnlockedZoneCount => _unlockedZoneCount;
        private TurretPowerBudget _budget;
        private bool _requiresMatchScope;
        private MatchScope _matchScope;
        public MatchId BoundMatchId => _matchScope?.MatchId ?? default;
        public bool HasWriteAuthority => !_requiresMatchScope ||
            (_matchScope != null && isActiveAndEnabled && _matchScope.HasAuthority());
        private sealed class MatchScope : IDisposable
        {
            private TurretController _owner;
            private readonly Func<bool> _authority;
            public MatchId MatchId { get; }
            public MatchScope(TurretController owner, MatchId matchId, Func<bool> authority)
            { _owner = owner; MatchId = matchId; _authority = authority; }
            public bool HasAuthority() => _owner != null && _authority();
            public void Dispose()
            {
                TurretController owner = _owner; _owner = null;
                if (owner != null) owner.ReleaseMatchScope(this);
            }
        }

        /// <summary>Core가 조립하는 Scene은 첫 Match 전과 종료 후에도 직접 상태 변경을 허용하지 않습니다.</summary>
        public void RequireMatchScope() => _requiresMatchScope = true;

        /// <summary>Feature 제어 수명만 임대합니다. 실제 생성자·체력·강화·개별 열 상태를 재초기화하지 않습니다.</summary>
        public IDisposable BindMatchScope(MatchId matchId, Func<bool> hasAuthority)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A Match ID is required.", nameof(matchId));
            if (hasAuthority == null) throw new ArgumentNullException(nameof(hasAuthority));
            if (_matchScope != null) throw new InvalidOperationException("The D controller already has a Match owner.");
            _requiresMatchScope = true; _nextAutoActivationTime = 0;
            _matchScope = new MatchScope(this, matchId, hasAuthority);
            return _matchScope;
        }

        private void ReleaseMatchScope(MatchScope scope)
        {
            if (!ReferenceEquals(_matchScope, scope)) return;
            // 먼저 권한과 자동 큐를 닫아 정리 이벤트에서 새 작업이 재진입하지 않게 합니다.
            _matchScope = null; IsAutoActivationRunning = false; _autoCandidates.Clear();
            List<Exception> errors = null;
            foreach (TurretBase turret in new List<TurretBase>(TurretInstanceRegistry.RegisteredInstances.Values))
            {
                if (!OwnsTurret(turret)) continue;
                try { turret.ReleaseMatchOperations(); }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            }
            if (errors != null) throw new AggregateException("D Match control cleanup failed.", errors);
        }
        public TurretPowerBudget Budget => _budget ??= new TurretPowerBudget(Mathf.Max(0, maximumPower));
        public int CurrentPower => Budget.CurrentPower;
        public int MaximumPower => Budget.MaximumPower;
        public IRegionGridSource Regions => _regionAccess?.Regions;
        public bool OwnsTurret(TurretBase turret) => turret != null &&
            turret.gameObject.scene == gameObject.scene && turret.UsesPowerSource(this);
        public event Action<int, int> PowerChanged
        {
            add => Budget.PowerChanged += value;
            remove => Budget.PowerChanged -= value;
        }

        private void Awake() { _ = Budget; }
        private void OnEnable()
        {
            TurretInstanceRegistry.Registered += ApplyUnlockZoneToNewTurret;
            if (_regionAccess != null)
            {
                _regionAccess.Regions.Changed += OnRegionChanged;
                ApplyRegionRestrictions();
            }
        }
        private void Update()
        {
            Budget.Tick(Time.deltaTime);
            if (HasWriteAuthority && IsAutoActivationRunning) AdvanceAutoActivation();
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
        public Func<TurretBase, int, float> AutoActivationScoreProvider { get; set; }

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
            if (!HasWriteAuthority || !isActiveAndEnabled || IsAutoActivationRunning || AutoActivationCooldownRemaining > 0f)
                return false;

            var owned = new List<TurretBase>();
            _autoCandidates.Clear();
            foreach (TurretBase turret in TurretInstanceRegistry.RegisteredInstances.Values)
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
            foreach (TurretBase turret in owned)
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
                if (!TurretInstanceRegistry.TryGet(candidate.InstanceId, out TurretBase turret) || turret == null ||
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
                TurretActivationResult result = turret.RequestSystemActivation(true);
                if (result == TurretActivationResult.CoolingDown)
                {
                    hasCoolingCandidate = true;
                    continue;
                }

                candidate.Completed = result != TurretActivationResult.InsufficientPower;
                if (!candidate.Completed) hasPowerPendingCandidate = true;
            }
            if (!hasCoolingCandidate &&
                (!hasPowerPendingCandidate || Budget.PendingRecovery == 0))
                IsAutoActivationRunning = false;
        }

        private void OnDisable()
        {
            TurretInstanceRegistry.Registered -= ApplyUnlockZoneToNewTurret;
            if (_regionAccess != null) _regionAccess.Regions.Changed -= OnRegionChanged;
            IsAutoActivationRunning = false;
            _matchScope?.Dispose();
        }

        public bool TrySetMaximumPower(int power)
        {
            if (!HasWriteAuthority) return false;
            if (!Budget.TrySetMaximumPower(power)) return false;
            maximumPower = power;
            return true;
        }

        public bool TryConsumePower(int power) => Budget.TryConsumePower(power);
        public bool TryChangeReservation(int previousPower, int newPower) => Budget.TryChangeReservation(previousPower, newPower);
        public void ReleasePower(int power) => Budget.ReleasePower(power);

        /// <summary>메인 스레드에서 호출한다. 같은 전력 공급자를 사용하는 등록 터렛만 집계한다.</summary>
        public TurretControllerSnapshot GetSnapshot()
        {
            int registered = 0;
            int activated = 0;
            int operational = 0;
            var activatedByType = new Dictionary<TurretKind, int>();
            foreach (TurretKind kind in Enum.GetValues(typeof(TurretKind)))
                activatedByType[kind] = 0;

            foreach (TurretBase turret in TurretInstanceRegistry.RegisteredInstances.Values)
            {
                if (turret == null || !turret.UsesPowerSource(this)) continue;
                registered++;
                if (turret.IsOperational) operational++;
                if (!turret.IsActivated || turret.IsDestroyed) continue;
                activated++;
                activatedByType.TryGetValue(turret.Kind, out int count);
                activatedByType[turret.Kind] = count + 1;
            }

            TurretPowerBudget budget = Budget;
            return new TurretControllerSnapshot(
                budget.MaximumPower, budget.CurrentPower, budget.ReservedPower, budget.PendingRecovery,
                registered, activated, operational, activatedByType);
        }

        public TurretActivationResult RequestActivation(int instanceId, bool shouldActivate)
        {
            if (!TurretInstanceRegistry.TryGet(instanceId, out TurretBase turret) || turret == null ||
                !turret.UsesPowerSource(this))
                return TurretActivationResult.PowerSourceUnavailable;
            return turret.RequestActivation(shouldActivate);
        }

        /// <summary>Builds the turret-owned grid from Inspector settings. Zone one starts unlocked.</summary>
        public bool ConfigureUnlockZones(Vector2 cuPosition)
            => ConfigureUnlockZones(cuPosition, unlockZoneCount);

        public bool ConfigureUnlockZones(Vector2 cuPosition, int zoneCount)
        {
            TurretGridConfiguration grid = CreateUnlockGridConfiguration();
            return ConfigureUnlockZones(grid, cuPosition, zoneCount);
        }

        /// <summary>Configures cell-distance bands. Zone one is available from the start.</summary>
        public bool ConfigureUnlockZones(TurretGridConfiguration grid, Vector2 cuPosition, int zoneCount = 4)
        {
            if (_regionAccess != null)
                throw new InvalidOperationException("The Match region policy owns access; update it through Core.");
            TurretUnlockZones zones = new TurretUnlockZones(grid, cuPosition, zoneCount);
            _unlockZones = zones;
            _unlockedZoneCount = 1;
            bool applied = true;
            foreach (TurretBase turret in TurretInstanceRegistry.RegisteredInstances.Values)
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
            foreach (TurretBase turret in TurretInstanceRegistry.RegisteredInstances.Values)
            {
                if (turret == null || turret.gameObject.scene != gameObject.scene ||
                    !turret.UsesPowerSource(this)) continue;
                applied &= ApplyUnlockZone(turret);
            }
            return applied;
        }

        public bool TryGetUnlockZone(TurretBase turret, out int zone)
        {
            zone = 0;
            return _unlockZones != null && turret != null &&
                   turret.gameObject.scene == gameObject.scene && turret.UsesPowerSource(this) &&
                   _unlockZones.TryGetZone(turret.transform.position, out zone);
        }

        internal bool CanUnlock(TurretBase turret)
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
        public TurretUnlockZones CreateUnlockZoneDefinition(Vector2 cuPosition)
        {
            return new TurretUnlockZones(CreateUnlockGridConfiguration(), cuPosition, unlockZoneCount);
        }

        /// <summary>CU 거리 정책과 별개로 동일한 지역 Grid 공간 정의를 읽습니다.</summary>
        public TurretGridConfiguration CreateUnlockGridConfiguration() => new TurretGridConfiguration(
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
            foreach (TurretBase turret in new List<TurretBase>(TurretInstanceRegistry.RegisteredInstances.Values))
            {
                if (_regionAccess == null) break;
                if (turret != null && turret.gameObject.scene == gameObject.scene && turret.UsesPowerSource(this))
                    ApplyUnlockZone(turret);
            }
        }

        private void ApplyUnlockZoneToNewTurret(TurretBase turret)
        {
            if ((_unlockZones != null || _regionAccess != null) && turret != null && turret.gameObject.scene == gameObject.scene &&
                turret.UsesPowerSource(this))
                ApplyUnlockZone(turret);
        }

        private bool ApplyUnlockZone(TurretBase turret)
        {
            bool shouldLock = !CanUnlock(turret);
            if (turret.IsLocked == shouldLock) return true;
            return turret.ApplyZoneLocked(shouldLock);
        }

        public static TurretController FindForScene(Scene scene)
        {
            foreach (TurretController controller in FindObjectsByType<TurretController>(FindObjectsSortMode.None))
                if (controller.gameObject.scene == scene && controller.isActiveAndEnabled) return controller;
            return null;
        }

        // Compatibility bootstrap: no CU or specially named object is required.
        public static TurretController GetOrCreateForScene(Scene scene)
        {
            TurretController controller = FindForScene(scene);
            if (controller != null) return controller;
            GameObject owner = new GameObject("TurretController");
            SceneManager.MoveGameObjectToScene(owner, scene);
            return owner.AddComponent<TurretController>();
        }
    }
}

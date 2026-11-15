// Scene의 터렛 전력·자동 활성화와 지역 사용 제한을 관리하는 FieldTurret 소유자입니다.
// Core 조립부에는 실제 소유 개체를 확인하는 읽기 API를 제공합니다.
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
    public sealed partial class FieldTurretController : MonoBehaviour, IFieldTurretPowerSource
    {
        [SerializeField, Min(0)] private int maximumPower = 250;
        public const float DefaultDestructionPowerReturnPercent = 50f;
        [SerializeField, Range(0f, 100f)] private float destructionPowerReturnPercent = DefaultDestructionPowerReturnPercent;
        public float DestructionPowerReturnPercent => IsValidReturnPercent(destructionPowerReturnPercent)
            ? destructionPowerReturnPercent : DefaultDestructionPowerReturnPercent;
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
        /// <summary>Core 정책이 연결된 정적 Match는 확정 해금을 읽습니다. 독립 Feature 시험은 기존 값을 사용합니다.</summary>
        public int UnlockedZoneCount
        {
            get
            {
                var snapshot = _regionAccess?.Regions.Snapshot;
                if (snapshot?.Mode != RegionPolicyMode.Static) return _unlockedZoneCount;
                int highest = 0;
                foreach (int id in snapshot.UnlockedRegionIds) highest = Math.Max(highest, id);
                return highest;
            }
        }
        private FieldTurretPowerBudget _budget;
        private bool _requiresMatchScope;
        private MatchScope _matchScope;
        public MatchId BoundMatchId => _matchScope?.MatchId ?? default;
        public bool HasWriteAuthority => !_requiresMatchScope ||
            (_matchScope != null && isActiveAndEnabled && _matchScope.HasAuthority());
        private sealed class MatchScope : IDisposable
        {
            private FieldTurretController _owner;
            private readonly Func<bool> _authority;
            public MatchId MatchId { get; }
            public MatchScope(FieldTurretController owner, MatchId matchId, Func<bool> authority)
            { _owner = owner; MatchId = matchId; _authority = authority; }
            public bool HasAuthority() => _owner != null && _authority();
            public void Dispose()
            {
                FieldTurretController owner = _owner; _owner = null;
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
            foreach (FieldTurretBase turret in new List<FieldTurretBase>(FieldTurretInstanceRegistry.RegisteredInstances.Values))
            {
                if (!OwnsTurret(turret)) continue;
                try { turret.ReleaseMatchOperations(); }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            }
            if (errors != null) throw new AggregateException("D Match control cleanup failed.", errors);
        }
        public FieldTurretPowerBudget Budget => _budget ??= new FieldTurretPowerBudget(Mathf.Max(0, maximumPower));
        public int CurrentPower => Budget.CurrentPower;
        public int MaximumPower => Budget.MaximumPower;
        public IRegionGridSource Regions => _regionAccess?.Regions;
        public bool OwnsTurret(FieldTurretBase turret) => turret != null &&
            turret.gameObject.scene == gameObject.scene && turret.UsesPowerSource(this);
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
            if (!HasWriteAuthority || !CanSimulate) return;
            Budget.Tick(TeamHJD.Game.Presentation.Scene.FieldSimulationTime.ScaleDelta(_fieldTime, Time.deltaTime));
            if (HasWriteAuthority && IsAutoActivationRunning) AdvanceAutoActivation();
        }

        public const float AutoActivationCooldownSeconds = 60f;
        private double _nextAutoActivationTime;
        private readonly List<AutoActivationCandidate> _autoCandidates = new();
        private IReadOnlyList<AutoActivationCandidate> _autoCandidatesView;
        public bool IsAutoActivationRunning { get; private set; }
        public float AutoActivationCooldownRemaining => (float)Math.Max(0, _nextAutoActivationTime - FieldSeconds);
        public IReadOnlyList<AutoActivationCandidate> AutoActivationCandidates =>
            _autoCandidatesView ??= _autoCandidates.AsReadOnly();

        // Optional future scoring policy. Evaluated only while capturing the button-time list.
        public Func<FieldTurretBase, int, float> AutoActivationScoreProvider { get; set; }

        public sealed class AutoActivationCandidate
        {
            public int InstanceId { get; }
            public int EnemyCount { get; }
            public float Score { get; }
            public float FirepowerEfficiency { get; }
            public TeamHJD.Game.Domain.EntityId EntityId { get; }
            internal bool Completed;
            internal AutoActivationCandidate(int id, int enemyCount, float score, float firepowerEfficiency, TeamHJD.Game.Domain.EntityId entityId = default)
            {
                InstanceId = id;
                EnemyCount = enemyCount;
                Score = score;
                FirepowerEfficiency = firepowerEfficiency;
                EntityId = entityId;
            }
        }

        /// <summary>CU interaction/button calls this once. No CU component is required.</summary>
        public bool TryStartAutoActivation()
        {
            if (!HasWriteAuthority || !isActiveAndEnabled || IsAutoActivationRunning || AutoActivationCooldownRemaining > 0f)
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
                float efficiency = turret.EffectivePower > 0 ? (float)turret.EffectiveDamage / turret.EffectivePower : 0f;
                turret.TryGetEntityId(out var identity);
                _autoCandidates.Add(new AutoActivationCandidate(turret.InstanceId, count, score, efficiency, identity));
            }
            _autoCandidates.Sort((left, right) =>
            {
                int byScore = right.Score.CompareTo(left.Score);
                if (byScore != 0) return byScore;
                int byEfficiency = right.FirepowerEfficiency.CompareTo(left.FirepowerEfficiency);
                return byEfficiency != 0 ? byEfficiency : left.InstanceId.CompareTo(right.InstanceId);
            });

            _nextAutoActivationTime = FieldSeconds + AutoActivationCooldownSeconds;
            IsAutoActivationRunning = true;
            foreach (FieldTurretBase turret in owned)
                if (turret.IsActivated) turret.RequestSystemActivation(false);
            AdvanceAutoActivation();
            return true;
        }

        private void AdvanceAutoActivation()
        {
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
                if ((long)turret.EffectivePower > (long)Budget.CurrentPower + Budget.PendingRecovery)
                {
                    candidate.Completed = true; // 반환이 끝나도 켤 수 없는 후보만 제외한다.
                    continue;
                }
                FieldTurretActivationResult result = turret.RequestSystemActivation(true);
                if (result == FieldTurretActivationResult.CoolingDown)
                {
                    return; // 앞선 후보의 냉각이 끝나기 전에는 다음 후보를 켜지 않는다.
                }

                if (result == FieldTurretActivationResult.InsufficientPower) return;
                candidate.Completed = true;
            }
            IsAutoActivationRunning = false;
        }

        private void OnDisable()
        {
            FieldTurretInstanceRegistry.Registered -= ApplyUnlockZoneToNewTurret;
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

        public bool TrySetDestructionPowerReturnPercent(float percent)
        {
            if (!IsValidReturnPercent(percent)) return false;
            destructionPowerReturnPercent = percent;
            return true;
        }

        private static bool IsValidReturnPercent(float percent) =>
            !float.IsNaN(percent) && !float.IsInfinity(percent) && percent >= 0f && percent <= 100f;

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

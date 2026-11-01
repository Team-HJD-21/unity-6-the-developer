// D의 실제 등록 개체와 Core Match 논리 ID를 연결하고 종료 때 구독/대응표를 해제합니다.
// 객체 생성·파괴·전력·공격은 D에 남기고 승급/강등의 ID 메타데이터만 이전합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Content;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.FieldTurrets;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    [DisallowMultipleComponent]
    public sealed class TurretMatchBinding : MonoBehaviour, ITurretEntityRegistration
    {
        [SerializeField] private FieldTurretController _controller;
        [SerializeField] private FieldTurretBattlefieldInputSource _battlefieldSource;
        [SerializeField] private FieldTurretLevelUpgradeCatalog _commandLevelCatalog;
        [SerializeField] private FieldTurretUpgradeDefinition[] _commandUpgrades = Array.Empty<FieldTurretUpgradeDefinition>();
        private MatchEntityRegistry<int> _registry;
        private TurretCommandService _commands;
        private IDisposable _featureScope;
        private readonly Dictionary<FieldTurretBase, EntityRegistration<int>> _actors = new Dictionary<FieldTurretBase, EntityRegistration<int>>();
        public MatchId BoundMatchId => _registry?.MatchId ?? default;
        public int Count => _registry?.Count ?? 0;
        public ITurretCommandService Commands => _commands;
        public FieldTurretController Controller => _controller;
        public IReadOnlyList<EntityRegistration<int>> Registrations => _registry?.CaptureRegistrations() ?? Array.Empty<EntityRegistration<int>>();

        public void BindCommands(Func<bool> isAuthority, Func<bool> isMatchRunning,
            Func<PlayerId, bool> isParticipant, ITurretCommandAuthorization authorization)
        {
            if (_registry == null || _commands != null) throw new InvalidOperationException("Bind one command service to a ready Match identity scope.");
            if (isAuthority == null || isMatchRunning == null) throw new ArgumentNullException(isAuthority == null ? nameof(isAuthority) : nameof(isMatchRunning));
            var executor = new TurretFeatureCommandExecutor(_controller, this, _commandLevelCatalog, _commandUpgrades,
                () => _registry != null && isAuthority() && isMatchRunning());
            _commands = new TurretCommandService(BoundMatchId, isAuthority, isMatchRunning, isParticipant, authorization, executor);
        }

        private void Awake()
        {
            // CORE-FEATURE-HOLD [TURRET-BIND] 2026-11-02: 미승인 Feature 수정 제외로 연결 보류.
            // 필요 API: RequireMatchScope. 계약 확정 후 아래 원본과 호출 경로를 함께 복구합니다.
#if false // CORE-FEATURE-HOLD: 원본 구현 보존; 임시로 컴파일에서 제외
            if (_controller != null) _controller.RequireMatchScope();
#endif
        }

        public void BindMatch(MatchId matchId, Func<bool> mayWrite = null)
        {
            // CORE-FEATURE-HOLD [TURRET-BIND] 2026-11-02: 미승인 Feature 수정 제외로 연결 보류.
            // 필요 API: OwnsTurret / TryGetEntityId / BindMatchScope / SetEntityIdResolver. 계약 확정 후 아래 원본과 호출 경로를 함께 복구합니다.
#if false // CORE-FEATURE-HOLD: 원본 구현 보존; 임시로 컴파일에서 제외
            if (_registry != null) throw new InvalidOperationException("Turret identities are already bound to a Match.");
            if (_controller == null || _battlefieldSource == null) throw new InvalidOperationException("Assign D controller and Battlefield source.");
            var registry = new MatchEntityRegistry<int>(matchId);
            try
            {
                foreach (FieldTurretBase turret in FieldTurretInstanceRegistry.GetAll())
                {
                    if (!_controller.OwnsTurret(turret)) continue;
                    if (!turret.TryGetEntityId(out EntityId id))
                        throw new InvalidOperationException($"Assign an authored entity ID before binding '{turret.name}'.");
                    _actors.Add(turret, registry.Register(id, turret.InstanceId));
                }
                _registry = registry;
                _featureScope = _controller.BindMatchScope(matchId, mayWrite ?? (() => true));
                FieldTurretInstanceRegistry.Registered += OnRegistered;
                FieldTurretInstanceRegistry.Unregistered += OnUnregistered;
                FieldTurretInstanceRegistry.LevelUpgraded += OnLevelReplaced;
                FieldTurretInstanceRegistry.LevelDowngraded += OnLevelReplaced;
                _battlefieldSource.SetEntityIdResolver(this);
            }
            catch
            {
                try { _featureScope?.Dispose(); }
                finally { _featureScope = null; registry.Dispose(); _actors.Clear(); _registry = null; }
                throw;
            }
#endif
            throw new NotSupportedException("CORE-FEATURE-HOLD: turret Match binding is awaiting the Feature contract.");
        }

        public bool TryResolveEntityId(int localInstanceId, out EntityId entityId)
        {
            entityId = default;
            if (_registry == null || !_registry.TryGetByHandle(localInstanceId, out var registration) ||
                !FieldTurretInstanceRegistry.TryGet(localInstanceId, out var turret) || turret == null || !_actors.TryGetValue(turret, out var owner) ||
                !ReferenceEquals(owner, registration)) return false;
            entityId = registration.EntityId; return true;
        }

        public bool TryResolveLocalId(EntityId entityId, out int localInstanceId)
        {
            localInstanceId = 0;
            if (_registry == null || !_registry.TryGetByEntityId(entityId, out var registration) ||
                !TryResolveEntityId(registration.Handle, out var current) || current != entityId) return false;
            localInstanceId = registration.Handle; return true;
        }

        public bool TryRegisterEntity(MatchId matchId, EntityId entityId, int localInstanceId, out string reason)
        {
            // CORE-FEATURE-HOLD [TURRET-BIND] 2026-11-02: 미승인 Feature 수정 제외로 연결 보류.
            // 필요 API: OwnsTurret / TryGetEntityId. 계약 확정 후 아래 원본과 호출 경로를 함께 복구합니다.
#if false // CORE-FEATURE-HOLD: 원본 구현 보존; 임시로 컴파일에서 제외
            reason = string.Empty;
            if (_registry == null || matchId != BoundMatchId) { reason = "MatchUnavailable"; return false; }
            if (entityId.IsEmpty || localInstanceId <= 0 || !FieldTurretInstanceRegistry.TryGet(localInstanceId, out var turret) ||
                !_controller.OwnsTurret(turret)) { reason = "TargetUnavailable"; return false; }
            if (turret.TryGetEntityId(out var existingId) && existingId != entityId)
            { reason = "IdentityConflict"; return false; }
            if ((_registry.TryGetByEntityId(entityId, out var existing) && existing.Handle != localInstanceId) ||
                (_registry.TryGetByHandle(localInstanceId, out existing) && existing.EntityId != entityId))
            { reason = "IdentityConflict"; return false; }
            var identity = turret.GetComponent<SceneEntityIdentity>();
            if (!turret.TryGetEntityId(out _))
            {
                if (identity == null) identity = turret.gameObject.AddComponent<SceneEntityIdentity>();
                identity.Initialize(entityId);
            }
            RegisterActor(turret, entityId);
            return true;
#endif
            reason = "CORE-FEATURE-HOLD";
            return false;
        }

        private void RegisterActor(FieldTurretBase turret, EntityId entityId)
        {
            // CORE-FEATURE-HOLD [TURRET-BIND] 2026-11-02: 미승인 Feature 수정 제외로 연결 보류.
            // 필요 API: RefreshLayout. 계약 확정 후 아래 원본과 호출 경로를 함께 복구합니다.
#if false // CORE-FEATURE-HOLD: 원본 구현 보존; 임시로 컴파일에서 제외
            EntityRegistration<int> registration = _registry.Register(entityId, turret.InstanceId);
            _actors[turret] = registration;
            _battlefieldSource.RefreshLayout();
#endif
            throw new NotSupportedException("CORE-FEATURE-HOLD: turret registration is awaiting the Feature contract.");
        }
        private void OnRegistered(FieldTurretBase turret)
        {
            // CORE-FEATURE-HOLD [TURRET-BIND] 2026-11-02: 미승인 Feature 수정 제외로 연결 보류.
            // 필요 API: OwnsTurret / TryGetEntityId. 계약 확정 후 아래 원본과 호출 경로를 함께 복구합니다.
#if false // CORE-FEATURE-HOLD: 원본 구현 보존; 임시로 컴파일에서 제외
            if (_registry == null || !_controller.OwnsTurret(turret)) return;
            // D Factory는 생성 후 TryRegisterEntity로 권한 생성자가 정한 ID를 전달할 수 있습니다.
            if (turret.TryGetEntityId(out EntityId id)) RegisterActor(turret, id);
#endif
        }
        private void OnUnregistered(FieldTurretBase turret)
        {
            // CORE-FEATURE-HOLD [TURRET-BIND] 2026-11-02: 미승인 Feature 수정 제외로 연결 보류.
            // 필요 API: RefreshLayout. 계약 확정 후 아래 원본과 호출 경로를 함께 복구합니다.
#if false // CORE-FEATURE-HOLD: 원본 구현 보존; 임시로 컴파일에서 제외
            if (!_actors.TryGetValue(turret, out var registration)) return;
            _actors.Remove(turret); _registry?.Unregister(registration); _battlefieldSource.RefreshLayout();
#endif
        }
        private void OnLevelReplaced(FieldTurretBase previous, FieldTurretBase current)
        {
            // CORE-FEATURE-HOLD [TURRET-BIND] 2026-11-02: 미승인 Feature 수정 제외로 연결 보류.
            // 필요 API: OwnsTurret / TryGetEntityId. 계약 확정 후 아래 원본과 호출 경로를 함께 복구합니다.
#if false // CORE-FEATURE-HOLD: 원본 구현 보존; 임시로 컴파일에서 제외
            if (_registry == null || !_controller.OwnsTurret(previous) || !_controller.OwnsTurret(current)) return;
            if (!previous.TryGetEntityId(out EntityId id)) throw new InvalidOperationException("The replaced turret has no logical identity.");
            if (!TryRegisterEntity(BoundMatchId, id, current.InstanceId, out string reason))
                throw new InvalidOperationException("Turret identity transfer failed: " + reason);
#endif
        }
        public void UnbindMatch(MatchId matchId)
        {
            if (_registry == null || matchId != BoundMatchId) return;
            _commands?.Dispose(); _commands = null;
            FieldTurretInstanceRegistry.Registered -= OnRegistered;
            FieldTurretInstanceRegistry.Unregistered -= OnUnregistered;
            FieldTurretInstanceRegistry.LevelUpgraded -= OnLevelReplaced;
            FieldTurretInstanceRegistry.LevelDowngraded -= OnLevelReplaced;
            MatchEntityRegistry<int> registry = _registry;
            _registry = null; _actors.Clear();
            try { _featureScope?.Dispose(); }
            finally
            {
                _featureScope = null;
                // CORE-FEATURE-HOLD [TURRET-BIND]: resolver 해제 API도 원본과 함께 보류합니다.
#if false // CORE-FEATURE-HOLD
                try { if (_battlefieldSource != null) _battlefieldSource.SetEntityIdResolver(null); }
                finally { registry.Dispose(); }
#else
                registry.Dispose();
#endif
            }
        }
        /// <summary>완료 화면 동안 ID/읽기는 보존하고 D의 자동 활성화·발사·예약 작업을 정리합니다.</summary>
        public void StopMatchOperations()
        {
            IDisposable featureScope = _featureScope;
            _featureScope = null;
            featureScope?.Dispose();
        }
        private void OnDestroy() => UnbindMatch(BoundMatchId);
    }
}

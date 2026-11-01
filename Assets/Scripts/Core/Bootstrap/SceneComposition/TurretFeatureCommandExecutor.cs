// 논리 대상을 D의 실제 개체로 해석하고 D API의 결과를 Core 계약으로 변환합니다.
// 전력/냉각/선택/승급 규칙과 실제 프리팹 교체는 재구현하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class TurretFeatureCommandExecutor : ITurretCommandExecutor
    {
        private readonly FieldTurretController _controller;
        private readonly ITurretEntityIdResolver _entities;
        private readonly FieldTurretLevelUpgradeCatalog _levels;
        private readonly Dictionary<string, FieldTurretUpgradeDefinition> _upgrades = new Dictionary<string, FieldTurretUpgradeDefinition>(StringComparer.Ordinal);
        private readonly Func<bool> _mayExecute;
        public TurretFeatureCommandExecutor(FieldTurretController controller, ITurretEntityIdResolver entities,
            FieldTurretLevelUpgradeCatalog levels, IEnumerable<FieldTurretUpgradeDefinition> upgrades, Func<bool> mayExecute)
        {
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _levels = levels != null ? levels : throw new ArgumentNullException(nameof(levels));
            _mayExecute = mayExecute ?? throw new ArgumentNullException(nameof(mayExecute));
            if (upgrades == null) throw new ArgumentNullException(nameof(upgrades));
            foreach (FieldTurretUpgradeDefinition upgrade in upgrades)
                if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.Id) || upgrade.Id.Length > 128 || !_upgrades.TryAdd(upgrade.Id, upgrade))
                    throw new ArgumentException("Upgrade definitions must have unique valid IDs.", nameof(upgrades));
        }
        public TurretCommandExecutionResult Execute(TurretCommandRequest request)
        {
            // CORE-FEATURE-HOLD [TURRET-COMMAND] 2026-11-02: 미승인 Feature 수정 제외로 연결 보류.
            // 필요 API: OwnsTurret. 계약 확정 후 아래 원본과 호출 경로를 함께 복구합니다.
#if false // CORE-FEATURE-HOLD: 원본 구현 보존; 임시로 컴파일에서 제외
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_controller == null || !_controller.isActiveAndEnabled || !_mayExecute() ||
                _entities.BoundMatchId.IsEmpty || request.Context.MatchId != _entities.BoundMatchId)
                return Result(TurretCommandStatus.TargetUnavailable, "FeatureUnavailable");
            if (request.Kind == TurretCommandKind.StartAutoActivation)
            {
                if (_controller.IsAutoActivationRunning) return Result(TurretCommandStatus.FeatureRejected, "AutoActivationInProgress");
                if (_controller.AutoActivationCooldownRemaining > 0) return Result(TurretCommandStatus.FeatureRejected, "AutoActivationCooldown");
                return _controller.TryStartAutoActivation()
                    ? Result(TurretCommandStatus.Accepted, "AutoActivationStarted", "The D operation started; not every turret is activated yet.")
                    : Result(TurretCommandStatus.FeatureRejected, "AutoActivationUnavailable");
            }
            if (!_entities.TryResolveLocalId(request.TargetId, out int localId) ||
                !FieldTurretInstanceRegistry.TryGet(localId, out FieldTurretBase turret) || turret == null || !_controller.OwnsTurret(turret))
                return Result(TurretCommandStatus.TargetUnavailable, "TargetUnavailable");
            if (request.Kind == TurretCommandKind.SetActivation)
            {
                FieldTurretActivationResult status = _controller.RequestActivation(localId, request.ShouldActivate);
                return Result(status == FieldTurretActivationResult.Activated || status == FieldTurretActivationResult.Deactivated
                    ? TurretCommandStatus.Applied : status == FieldTurretActivationResult.Unchanged
                        ? TurretCommandStatus.Unchanged : TurretCommandStatus.FeatureRejected, status.ToString());
            }
            if (request.Kind == TurretCommandKind.UpgradeLevel || request.Kind == TurretCommandKind.DowngradeLevel)
            {
                bool increase = request.Kind == TurretCommandKind.UpgradeLevel;
                FieldTurretBase prefab;
                bool found = increase ? _levels.TryGetNext(turret.Definition, out prefab) : _levels.TryGetPrevious(turret.Definition, out prefab);
                if (!found || prefab == null) return Result(TurretCommandStatus.FeatureRejected, "LevelTransitionUnavailable");
                FieldTurretLevelUpgradeResult status = increase ? turret.RequestLevelUpgrade(prefab, out _) : turret.RequestLevelDowngrade(prefab, out _);
                return Result(status == FieldTurretLevelUpgradeResult.Upgraded || status == FieldTurretLevelUpgradeResult.Downgraded
                    ? TurretCommandStatus.Applied : TurretCommandStatus.FeatureRejected, status.ToString());
            }
            if (!_upgrades.TryGetValue(request.UpgradeId.Value, out FieldTurretUpgradeDefinition definition) || definition == null)
                return Result(TurretCommandStatus.FeatureRejected, "UpgradeDefinitionUnavailable");
            FieldTurretUpgradeResult upgradeResult = request.Kind == TurretCommandKind.ApplyUpgrade
                ? turret.ApplyUpgrade(definition) : turret.DowngradeUpgrade(definition);
            return Result(upgradeResult == FieldTurretUpgradeResult.Applied || upgradeResult == FieldTurretUpgradeResult.Downgraded
                ? TurretCommandStatus.Applied : TurretCommandStatus.FeatureRejected, upgradeResult.ToString());
#endif
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Result(TurretCommandStatus.TargetUnavailable, "CORE-FEATURE-HOLD", "Turret Feature integration is on hold.");
        }
        private static TurretCommandExecutionResult Result(TurretCommandStatus status, string code, string reason = "")
            => new TurretCommandExecutionResult(status, code, reason);
    }
}

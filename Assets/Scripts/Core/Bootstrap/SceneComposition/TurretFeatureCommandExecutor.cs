// 논리 대상을 D의 실제 개체로 해석하고 D API의 결과를 Core 계약으로 변환합니다.
// 전력/냉각/선택/승급 규칙과 실제 프리팹 교체는 재구현하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class TurretFeatureCommandExecutor : ITurretCommandExecutor
    {
        private readonly TurretController _controller;
        private readonly ITurretEntityIdResolver _entities;
        private readonly TurretLevelUpgradeCatalog _levels;
        private readonly Dictionary<string, TurretUpgradeDefinition> _upgrades = new Dictionary<string, TurretUpgradeDefinition>(StringComparer.Ordinal);
        private readonly Func<bool> _mayExecute;
        public TurretFeatureCommandExecutor(TurretController controller, ITurretEntityIdResolver entities,
            TurretLevelUpgradeCatalog levels, IEnumerable<TurretUpgradeDefinition> upgrades, Func<bool> mayExecute)
        {
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _levels = levels != null ? levels : throw new ArgumentNullException(nameof(levels));
            _mayExecute = mayExecute ?? throw new ArgumentNullException(nameof(mayExecute));
            if (upgrades == null) throw new ArgumentNullException(nameof(upgrades));
            foreach (TurretUpgradeDefinition upgrade in upgrades)
                if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.Id) || upgrade.Id.Length > 128 || !_upgrades.TryAdd(upgrade.Id, upgrade))
                    throw new ArgumentException("Upgrade definitions must have unique valid IDs.", nameof(upgrades));
        }
        public TurretCommandExecutionResult Execute(TurretCommandRequest request)
        {
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
                !TurretInstanceRegistry.TryGet(localId, out TurretBase turret) || turret == null || !_controller.OwnsTurret(turret))
                return Result(TurretCommandStatus.TargetUnavailable, "TargetUnavailable");
            if (request.Kind == TurretCommandKind.SetActivation)
            {
                TurretActivationResult status = _controller.RequestActivation(localId, request.ShouldActivate);
                return Result(status == TurretActivationResult.Activated || status == TurretActivationResult.Deactivated
                    ? TurretCommandStatus.Applied : status == TurretActivationResult.Unchanged
                        ? TurretCommandStatus.Unchanged : TurretCommandStatus.FeatureRejected, status.ToString());
            }
            if (request.Kind == TurretCommandKind.UpgradeLevel || request.Kind == TurretCommandKind.DowngradeLevel)
            {
                bool increase = request.Kind == TurretCommandKind.UpgradeLevel;
                TurretBase prefab;
                bool found = increase ? _levels.TryGetNext(turret.Definition, out prefab) : _levels.TryGetPrevious(turret.Definition, out prefab);
                if (!found || prefab == null) return Result(TurretCommandStatus.FeatureRejected, "LevelTransitionUnavailable");
                TurretLevelUpgradeResult status = increase ? turret.RequestLevelUpgrade(prefab, out _) : turret.RequestLevelDowngrade(prefab, out _);
                return Result(status == TurretLevelUpgradeResult.Upgraded || status == TurretLevelUpgradeResult.Downgraded
                    ? TurretCommandStatus.Applied : TurretCommandStatus.FeatureRejected, status.ToString());
            }
            if (!_upgrades.TryGetValue(request.UpgradeId.Value, out TurretUpgradeDefinition definition) || definition == null)
                return Result(TurretCommandStatus.FeatureRejected, "UpgradeDefinitionUnavailable");
            TurretUpgradeResult upgradeResult = request.Kind == TurretCommandKind.ApplyUpgrade
                ? turret.ApplyUpgrade(definition) : turret.DowngradeUpgrade(definition);
            return Result(upgradeResult == TurretUpgradeResult.Applied || upgradeResult == TurretUpgradeResult.Downgraded
                ? TurretCommandStatus.Applied : TurretCommandStatus.FeatureRejected, upgradeResult.ToString());
        }
        private static TurretCommandExecutionResult Result(TurretCommandStatus status, string code, string reason = "")
            => new TurretCommandExecutionResult(status, code, reason);
    }
}

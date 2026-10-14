using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets;

/// <summary>버튼 표시와 요청 직전 검증에서 공유하는 조작 조건입니다.</summary>
internal static class StageTurretInteractionRules
{
    public static bool CanToggle(TurretBase turret) => turret != null &&
        turret.isActiveAndEnabled && turret.IsInitialized && !turret.IsManualOperationBlocked &&
        (turret.IsActivated || (!turret.IsLocked && !turret.IsDestroyed && !turret.IsCoolingDown));

    public static bool CanChangeUpgrade(TurretBase turret, TurretUpgradeDefinition upgrade, bool increase)
    {
        if (turret == null || !turret.CanModifyUpgrades || upgrade == null ||
            turret.Definition == null || !upgrade.IsCompatibleWith(turret.Definition.Id)) return false;

        var state = turret.RuntimeState;
        bool selected = state.HasAppliedUpgrade(upgrade.Id);
        return increase
            ? (state.UpgradeLevel == 0 || selected) && state.UpgradeLevel < TurretRuntimeState.MaximumUpgradeLevel
            : selected;
    }
}

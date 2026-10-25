using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Contracts;

/// <summary>버튼 표시와 요청 직전 검증에서 공유하는 조작 조건입니다.</summary>
internal static class StageTurretInteractionRules
{
    public static bool CanToggle(TurretPresentationSnapshot turret) => turret != null && turret.IsInitialized &&
        !turret.IsManualOperationBlocked && (turret.IsActivated || (!turret.IsLocked && !turret.IsDestroyed && !turret.IsCoolingDown));
    public static bool CanChangeUpgrade(TurretPresentationSnapshot turret, TurretUpgradeDefinition upgrade, bool increase)
    {
        if (turret == null || !turret.CanModifyUpgrades || upgrade == null || !upgrade.IsCompatibleWith(turret.DefinitionId.Value)) return false;
        bool selected = turret.SelectedUpgradeId.Value == upgrade.Id;
        return increase ? (turret.UpgradeLevel == 0 || selected) && turret.UpgradeLevel < TurretRuntimeState.MaximumUpgradeLevel : selected;
    }
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

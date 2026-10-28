using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Contracts;

/// <summary>버튼 표시와 요청 직전 검증에서 공유하는 조작 조건입니다.</summary>
internal static class StageTurretInteractionRules
{
    public static bool CanToggle(TurretPresentationSnapshot turret) => turret != null && turret.IsInitialized &&
        !turret.IsManualOperationBlocked && (turret.IsActivated || (!turret.IsLocked && !turret.IsDestroyed && !turret.IsCoolingDown));
    public static bool CanChangeUpgrade(TurretPresentationSnapshot turret, FieldTurretUpgradeDefinition upgrade, bool increase)
    {
        if (turret == null || !turret.CanModifyUpgrades || upgrade == null || !upgrade.IsCompatibleWith(turret.DefinitionId.Value)) return false;
        bool selected = turret.SelectedUpgradeId.Value == upgrade.Id;
        return increase ? (turret.UpgradeLevel == 0 || selected) && turret.UpgradeLevel < FieldTurretRuntimeState.MaximumUpgradeLevel : selected;
    }
    public static bool CanToggle(FieldTurretBase turret) => turret != null &&
        turret.isActiveAndEnabled && turret.IsInitialized && !turret.IsManualOperationBlocked &&
        (turret.IsActivated || (!turret.IsLocked && !turret.IsDestroyed && !turret.IsCoolingDown));

    public static bool CanChangeUpgrade(FieldTurretBase turret, FieldTurretUpgradeDefinition upgrade, bool increase)
    {
        if (turret == null || !turret.CanModifyUpgrades || upgrade == null ||
            turret.Definition == null || !upgrade.IsCompatibleWith(turret.Definition.Id)) return false;

        var state = turret.RuntimeState;
        bool selected = state.HasAppliedUpgrade(upgrade.Id);
        return increase
            ? (state.UpgradeLevel == 0 || selected) && state.UpgradeLevel < FieldTurretRuntimeState.MaximumUpgradeLevel
            : selected;
    }
}

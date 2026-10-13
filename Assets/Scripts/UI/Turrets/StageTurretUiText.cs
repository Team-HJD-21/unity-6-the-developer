using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;

/// <summary>터렛 API 요청 결과의 표시 문구를 관리합니다.</summary>
internal static class StageTurretUiText
{
    public static string Activation(TurretActivationResult result) => result switch
    {
        TurretActivationResult.Activated => "Turret activated.",
        TurretActivationResult.Deactivated => "Turret deactivated.",
        TurretActivationResult.Unchanged => "Already in the requested state.",
        TurretActivationResult.Destroyed => "This turret is destroyed.",
        TurretActivationResult.Locked => "This turret is locked.",
        TurretActivationResult.CoolingDown => "Waiting for activation cooldown.",
        TurretActivationResult.InsufficientPower => "Not enough available power.",
        TurretActivationResult.AutoActivationInProgress => "Automatic activation is in progress.",
        TurretActivationResult.PowerSourceUnavailable => "Turret power source unavailable.",
        TurretActivationResult.InvalidPowerCost => "Invalid turret power cost.",
        _ => "Unable to process activation request."
    };

    public static string Level(TurretLevelUpgradeResult result) => result switch
    {
        TurretLevelUpgradeResult.Upgraded => "Level increased.",
        TurretLevelUpgradeResult.Downgraded => "Level decreased.",
        TurretLevelUpgradeResult.Destroyed => "This turret is destroyed.",
        TurretLevelUpgradeResult.Locked => "This turret is locked.",
        TurretLevelUpgradeResult.NotInitialized => "Waiting for turret initialization.",
        TurretLevelUpgradeResult.InsufficientPower => "Not enough power for this level change.",
        TurretLevelUpgradeResult.PowerSourceUnavailable => "Turret power source unavailable.",
        TurretLevelUpgradeResult.InvalidNextLevel => "Cannot upgrade to this level.",
        TurretLevelUpgradeResult.InvalidPreviousLevel => "Cannot downgrade to this level.",
        TurretLevelUpgradeResult.ReplacementInitializationFailed => "Replacement turret initialization failed.",
        _ => "Unable to process level change request."
    };
}

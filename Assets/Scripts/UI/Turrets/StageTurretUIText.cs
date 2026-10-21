using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;

/// <summary>터렛 API 요청 결과의 표시 문구를 관리합니다.</summary>
internal static class StageTurretUIText
{
    public const string OutOfReach = "Move closer to control this turret.";

    public static string WithCooling(string message, TurretBase turret) => turret.IsCoolingDown
        ? $"{message} | Cooling {turret.CoolingDownRemaining:0.0}s"
        : message;

    public static string UpgradeRestriction(TurretBase turret)
    {
        if (!turret.IsInitialized) return "Upgrades: waiting for initialization";
        if (turret.IsManualOperationBlocked)
            return WithCooling("Upgrades: automatic activation in progress", turret);
        if (turret.IsDestroyed) return "Upgrades: turret destroyed";
        if (turret.IsLocked) return "Upgrades: zone locked";
        if (turret.IsCoolingDown) return $"Upgrades: cooling down ({turret.CoolingDownRemaining:0.0}s)";
        if (turret.IsActivated) return "Upgrades: deactivate the turret first";
        return turret.RuntimeState.UpgradeLevel == 0
            ? "Choose one upgrade branch" : "Reduce the selected branch to 0 to switch";
    }

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
        TurretLevelUpgradeResult.RequiresDeactivation => "Deactivate the turret before changing level.",
        TurretLevelUpgradeResult.CoolingDown => "Wait until cooling finishes before changing level.",
        TurretLevelUpgradeResult.AutoActivationInProgress => "Automatic activation is in progress.",
        _ => "Unable to process level change request."
    };

    public static string Upgrade(TurretUpgradeResult result) => result switch
    {
        TurretUpgradeResult.Applied => "Upgrade increased.",
        TurretUpgradeResult.Downgraded => "Upgrade decreased.",
        TurretUpgradeResult.RequiresDeactivation => "Deactivate the turret before upgrading.",
        TurretUpgradeResult.CoolingDown => "Wait until cooling finishes before upgrading.",
        TurretUpgradeResult.AutoActivationInProgress => "Automatic activation is in progress.",
        TurretUpgradeResult.BranchLocked => "Reduce the selected branch to 0 to switch.",
        TurretUpgradeResult.MaximumLevel => "Maximum upgrade level reached.",
        TurretUpgradeResult.MinimumLevel => "No upgrade to remove.",
        TurretUpgradeResult.Locked => "This turret is locked.",
        TurretUpgradeResult.Destroyed => "This turret is destroyed.",
        TurretUpgradeResult.NotInitialized => "Waiting for turret initialization.",
        TurretUpgradeResult.IncompatibleDefinition => "Upgrade incompatible with this turret.",
        TurretUpgradeResult.MissingDefinition => "Upgrade definition unavailable.",
        TurretUpgradeResult.InvalidUpgradeId => "Invalid upgrade ID.",
        TurretUpgradeResult.InsufficientPower => "Not enough available power.",
        TurretUpgradeResult.PowerSourceUnavailable => "Turret power source unavailable.",
        TurretUpgradeResult.InvalidPowerCost => "Invalid turret power cost.",
        TurretUpgradeResult.AlreadyApplied => "Already in the requested upgrade state.",
        _ => "Unable to process upgrade request."
    };
}

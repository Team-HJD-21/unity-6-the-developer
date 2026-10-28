using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using TeamHJD.Game.Contracts;

/// <summary>터렛 API 요청 결과의 표시 문구를 관리합니다.</summary>
internal static class StageTurretUIText
{
    public const string OutOfReach = "Move closer to control this turret.";

    public static string Command(TurretCommandResult result)
    {
        if (result.Status == TurretCommandStatus.Accepted) return "Automatic activation accepted by Host.";
        if (result.Status == TurretCommandStatus.Applied) return "Applied by Host.";
        if (result.Status == TurretCommandStatus.Unchanged) return "Already in the requested state.";
        if (result.Status == TurretCommandStatus.ExecutionFailed) return "Host execution failed. Inspect the updated state before retrying.";
        return result.FeatureCode switch
        {
            "AuthorityActorUnavailable" => "Waiting for the Host's verified player actor.",
            "OutOfInteractionRange" => "Move closer to the target (Host validation).",
            "PlayerDead" => "Cannot interact while dead.",
            "InteractionBlocked" => "Player interaction is currently blocked.",
            "InteractionTargetUnavailable" => "Interaction target is unavailable.",
            _ => $"Host rejected: {(string.IsNullOrEmpty(result.FeatureCode) ? result.Status.ToString() : result.FeatureCode)}"
        };
    }
    public static string UpgradeRestriction(TurretPresentationSnapshot turret)
    {
        if (turret == null || !turret.IsInitialized) return "Upgrades: waiting for Host state";
        if (turret.IsManualOperationBlocked) return "Upgrades: automatic activation in progress";
        if (turret.IsDestroyed) return "Upgrades: turret destroyed";
        if (turret.IsLocked) return "Upgrades: zone locked";
        if (turret.IsCoolingDown) return $"Upgrades: cooling down ({turret.CoolingRemaining:0.0}s)";
        if (turret.IsActivated) return "Upgrades: deactivate the turret first";
        return turret.UpgradeLevel == 0 ? "Choose one upgrade branch" : "Reduce the selected branch to 0 to switch";
    }
    public static string WithCooling(string message, FieldTurretBase turret) => turret.IsCoolingDown
        ? $"{message} | Cooling {turret.CoolingDownRemaining:0.0}s"
        : message;

    public static string UpgradeRestriction(FieldTurretBase turret)
    {
        if (!turret.IsInitialized) return "Upgrades: waiting for initialization";
        if (!turret.HasStateAuthority) return "Upgrades: requires server authority";
        if (turret.IsManualOperationBlocked)
            return WithCooling("Upgrades: automatic activation in progress", turret);
        if (turret.IsDestroyed) return "Upgrades: turret destroyed";
        if (turret.IsLocked) return "Upgrades: zone locked";
        if (turret.IsCoolingDown) return $"Upgrades: cooling down ({turret.CoolingDownRemaining:0.0}s)";
        if (turret.IsActivated) return "Upgrades: deactivate the turret first";
        return turret.RuntimeState.UpgradeLevel == 0
            ? "Choose one upgrade branch" : "Reduce the selected branch to 0 to switch";
    }

    public static string Activation(FieldTurretActivationResult result) => result switch
    {
        FieldTurretActivationResult.Activated => "Turret activated.",
        FieldTurretActivationResult.Deactivated => "Turret deactivated.",
        FieldTurretActivationResult.Unchanged => "Already in the requested state.",
        FieldTurretActivationResult.Destroyed => "This turret is destroyed.",
        FieldTurretActivationResult.Locked => "This turret is locked.",
        FieldTurretActivationResult.CoolingDown => "Waiting for activation cooldown.",
        FieldTurretActivationResult.InsufficientPower => "Not enough available power.",
        FieldTurretActivationResult.AutoActivationInProgress => "Automatic activation is in progress.",
        FieldTurretActivationResult.PowerSourceUnavailable => "Turret power source unavailable.",
        FieldTurretActivationResult.InvalidPowerCost => "Invalid turret power cost.",
        FieldTurretActivationResult.NotAuthoritative => "Requires server authority.",
        _ => "Unable to process activation request."
    };

    public static string Level(FieldTurretLevelUpgradeResult result) => result switch
    {
        FieldTurretLevelUpgradeResult.Upgraded => "Level increased.",
        FieldTurretLevelUpgradeResult.Downgraded => "Level decreased.",
        FieldTurretLevelUpgradeResult.Destroyed => "This turret is destroyed.",
        FieldTurretLevelUpgradeResult.Locked => "This turret is locked.",
        FieldTurretLevelUpgradeResult.NotInitialized => "Waiting for turret initialization.",
        FieldTurretLevelUpgradeResult.InsufficientPower => "Not enough power for this level change.",
        FieldTurretLevelUpgradeResult.PowerSourceUnavailable => "Turret power source unavailable.",
        FieldTurretLevelUpgradeResult.InvalidNextLevel => "Cannot upgrade to this level.",
        FieldTurretLevelUpgradeResult.InvalidPreviousLevel => "Cannot downgrade to this level.",
        FieldTurretLevelUpgradeResult.ReplacementInitializationFailed => "Replacement turret initialization failed.",
        FieldTurretLevelUpgradeResult.RequiresDeactivation => "Deactivate the turret before changing level.",
        FieldTurretLevelUpgradeResult.CoolingDown => "Wait until cooling finishes before changing level.",
        FieldTurretLevelUpgradeResult.AutoActivationInProgress => "Automatic activation is in progress.",
        _ => "Unable to process level change request."
    };

    public static string Upgrade(FieldTurretUpgradeResult result) => result switch
    {
        FieldTurretUpgradeResult.Applied => "Upgrade increased.",
        FieldTurretUpgradeResult.Downgraded => "Upgrade decreased.",
        FieldTurretUpgradeResult.RequiresDeactivation => "Deactivate the turret before upgrading.",
        FieldTurretUpgradeResult.CoolingDown => "Wait until cooling finishes before upgrading.",
        FieldTurretUpgradeResult.AutoActivationInProgress => "Automatic activation is in progress.",
        FieldTurretUpgradeResult.BranchLocked => "Reduce the selected branch to 0 to switch.",
        FieldTurretUpgradeResult.MaximumLevel => "Maximum upgrade level reached.",
        FieldTurretUpgradeResult.MinimumLevel => "No upgrade to remove.",
        FieldTurretUpgradeResult.Locked => "This turret is locked.",
        FieldTurretUpgradeResult.Destroyed => "This turret is destroyed.",
        FieldTurretUpgradeResult.NotInitialized => "Waiting for turret initialization.",
        FieldTurretUpgradeResult.IncompatibleDefinition => "Upgrade incompatible with this turret.",
        FieldTurretUpgradeResult.MissingDefinition => "Upgrade definition unavailable.",
        FieldTurretUpgradeResult.InvalidUpgradeId => "Invalid upgrade ID.",
        FieldTurretUpgradeResult.InsufficientPower => "Not enough available power.",
        FieldTurretUpgradeResult.PowerSourceUnavailable => "Turret power source unavailable.",
        FieldTurretUpgradeResult.InvalidPowerCost => "Invalid turret power cost.",
        FieldTurretUpgradeResult.AlreadyApplied => "Already in the requested upgrade state.",
        _ => "Unable to process upgrade request."
    };
}

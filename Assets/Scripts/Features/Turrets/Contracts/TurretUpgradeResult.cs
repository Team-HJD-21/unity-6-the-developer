namespace TeamHJD.Game.Turrets.Contracts
{
    public enum TurretUpgradeResult
    {
        Applied,
        MissingDefinition,
        InvalidUpgradeId,
        IncompatibleDefinition,
        AlreadyApplied,
        Destroyed,
        Locked,
        NotInitialized,
        InsufficientPower,
        PowerSourceUnavailable,
        InvalidPowerCost,
        Downgraded,
        BranchLocked,
        MaximumLevel,
        MinimumLevel,
        RequiresDeactivation,
        CoolingDown,
        AutoActivationInProgress,
        NotAuthoritative
    }
}

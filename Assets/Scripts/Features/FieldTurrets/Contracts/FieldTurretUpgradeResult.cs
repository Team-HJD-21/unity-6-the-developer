namespace TeamHJD.Game.FieldTurrets.Contracts
{
    public enum FieldTurretUpgradeResult
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
        AutoActivationInProgress
    }
}

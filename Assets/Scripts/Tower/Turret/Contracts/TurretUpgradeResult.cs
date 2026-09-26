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
        NotInitialized,
        InsufficientPower,
        PowerSourceUnavailable,
        InvalidPowerCost
    }
}

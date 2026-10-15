namespace TeamHJD.Game.Turrets.Contracts
{
    public enum TurretActivationResult
    {
        Activated,
        Deactivated,
        Unchanged,
        Destroyed,
        Locked,
        CoolingDown,
        InsufficientPower,
        PowerSourceUnavailable,
        InvalidPowerCost,
        AutoActivationInProgress,
        NotAuthoritative
    }
}

namespace TeamHJD.Game.FieldTurrets.Contracts
{
    public enum FieldTurretActivationResult
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

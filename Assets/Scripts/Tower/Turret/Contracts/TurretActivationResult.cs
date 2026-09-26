namespace TeamHJD.Game.Turrets.Contracts
{
    public enum TurretActivationResult
    {
        Activated,
        Deactivated,
        Unchanged,
        Destroyed,
        InsufficientPower,
        PowerSourceUnavailable,
        InvalidPowerCost
    }
}

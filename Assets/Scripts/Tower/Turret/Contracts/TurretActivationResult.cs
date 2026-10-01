namespace TeamHJD.Game.Turrets.Contracts
{
    public enum TurretActivationResult
    {
        Activated,
        Deactivated,
        Unchanged,
        Destroyed,
        Locked,
        InsufficientPower,
        PowerSourceUnavailable,
        InvalidPowerCost
    }
}

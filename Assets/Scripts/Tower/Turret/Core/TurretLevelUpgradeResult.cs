namespace TeamHJD.Game.Turrets
{
    public enum TurretLevelUpgradeResult
    {
        Upgraded,
        Downgraded,
        Destroyed,
        NotInitialized,
        InvalidNextLevel,
        InvalidPreviousLevel,
        InsufficientPower,
        PowerSourceUnavailable,
        ReplacementInitializationFailed
    }
}

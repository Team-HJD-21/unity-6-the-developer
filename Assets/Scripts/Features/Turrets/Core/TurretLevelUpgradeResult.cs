namespace TeamHJD.Game.Turrets
{
    public enum TurretLevelUpgradeResult
    {
        Upgraded,
        Downgraded,
        Destroyed,
        Locked,
        NotInitialized,
        InvalidNextLevel,
        InvalidPreviousLevel,
        InsufficientPower,
        PowerSourceUnavailable,
        ReplacementInitializationFailed,
        RequiresDeactivation,
        CoolingDown,
        AutoActivationInProgress,
        NotAuthoritative
    }
}

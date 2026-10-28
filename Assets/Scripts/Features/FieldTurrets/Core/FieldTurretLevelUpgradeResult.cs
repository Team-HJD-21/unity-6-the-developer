namespace TeamHJD.Game.FieldTurrets
{
    public enum FieldTurretLevelUpgradeResult
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

namespace TeamHJD.Game.Turrets.Contracts
{
    /// <summary>Outcome of one caller-driven repair tick. Accumulating is valid progress below one HP.</summary>
    public enum TurretRepairResult
    {
        Repaired,
        Accumulating,
        FullHealth,
        NotInitialized,
        Inactive,
        Locked,
        Destroyed,
        AutoActivationInProgress,
        InvalidRepairRate,
        InvalidDeltaTime
    }
}

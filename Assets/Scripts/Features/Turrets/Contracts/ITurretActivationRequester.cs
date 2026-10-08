namespace TeamHJD.Game.Turrets.Contracts
{
    public interface ITurretActivationRequester
    {
        bool IsActivated { get; }
        TurretActivationResult RequestActivation(bool shouldActivate);
    }
}

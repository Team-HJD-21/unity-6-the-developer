namespace TeamHJD.Game.FieldTurrets.Contracts
{
    public interface IFieldTurretActivationRequester
    {
        bool IsActivated { get; }
        FieldTurretActivationResult RequestActivation(bool shouldActivate);
    }
}

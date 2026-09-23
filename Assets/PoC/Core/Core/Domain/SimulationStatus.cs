// Describes whether the current simulation shell processed a command.

namespace TeamHJD.Game.Domain
{
    public enum SimulationStatus
    {
        NotHandled,
        Applied,
        Rejected
    }
}

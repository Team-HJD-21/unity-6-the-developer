// Requests the start of the next wave for a match.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class StartWaveCommand : GameCommand
    {
        public StartWaveCommand(Guid commandId, PlayerId issuerId) : base(commandId, issuerId) { }
    }
}

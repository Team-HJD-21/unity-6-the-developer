// Base data for an actor's request to change match state.


using System;

namespace TeamHJD.Game.Domain
{
    public abstract class GameCommand
    {
        public Guid CommandId { get; }
        public PlayerId IssuerId { get; }

        protected GameCommand(Guid commandId, PlayerId issuerId)
        {
            CommandId = commandId;
            IssuerId = issuerId;
        }
    }
}

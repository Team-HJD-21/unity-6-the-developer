// Requests interaction with a domain entity or scene-bound target.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class InteractCommand : GameCommand
    {
        public EntityId TargetId { get; }

        public InteractCommand(Guid commandId, PlayerId issuerId, EntityId targetId)
            : base(commandId, issuerId) => TargetId = targetId;
    }
}

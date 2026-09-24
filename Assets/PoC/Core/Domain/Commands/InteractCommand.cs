// Domain 개체 또는 Scene 대상과의 상호작용을 요청합니다.


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

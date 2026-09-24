// 배치된 방어 개체의 활성화를 요청합니다.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class ActivateTurretCommand : GameCommand
    {
        public EntityId TurretId { get; }

        public ActivateTurretCommand(Guid commandId, PlayerId issuerId, EntityId turretId)
            : base(commandId, issuerId) => TurretId = turretId;
    }
}

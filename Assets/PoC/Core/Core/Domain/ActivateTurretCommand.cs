// Requests activation of a placed defense entity.


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

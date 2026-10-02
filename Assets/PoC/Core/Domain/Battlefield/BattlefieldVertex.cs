// Territory topology에서 터렛 정점의 ID와 투영 좌표를 함께 보존합니다.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldVertex
    {
        public EntityId EntityId { get; }
        public BattlefieldPoint Position { get; }

        internal BattlefieldVertex(EntityId entityId, BattlefieldPoint position)
        {
            if (entityId.IsEmpty) throw new ArgumentException("A battlefield vertex requires a valid entity ID.", nameof(entityId));
            EntityId = entityId;
            Position = position;
        }
    }
}

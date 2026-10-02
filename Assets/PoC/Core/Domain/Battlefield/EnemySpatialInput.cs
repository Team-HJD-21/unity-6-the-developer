// Enemy의 ID와 XY 위치만 Battlefield 분석에 전달합니다.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class EnemySpatialInput
    {
        public EntityId EntityId { get; }
        public BattlefieldPoint Position { get; }

        public EnemySpatialInput(EntityId entityId, BattlefieldPoint position)
        {
            if (entityId.IsEmpty) throw new ArgumentException("Spatial input requires a valid enemy ID.", nameof(entityId));
            EntityId = entityId;
            Position = position;
        }
    }
}

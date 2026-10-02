// 전장 계산에 필요한 터렛 ID와 평면 위치만 전달하는 불변 입력 값입니다.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class TurretSpatialInput
    {
        public EntityId EntityId { get; }
        public BattlefieldPoint Position { get; }

        public TurretSpatialInput(EntityId entityId, BattlefieldPoint position)
        {
            if (entityId.IsEmpty) throw new ArgumentException("Spatial input requires a valid turret ID.", nameof(entityId));
            EntityId = entityId;
            Position = position;
        }
    }
}

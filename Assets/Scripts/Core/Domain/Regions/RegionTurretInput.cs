// 지역 정책에 필요한 터렛의 ID·위치·활성/파괴 값만 전달합니다.
// 발사 가능 여부나 과열은 활성 상태를 대신하지 않습니다.
using System;

namespace TeamHJD.Game.Domain
{
    public sealed class RegionTurretInput
    {
        public EntityId EntityId { get; }
        public BattlefieldPoint Position { get; }
        public bool IsActivated { get; }
        public bool IsDestroyed { get; }
        public RegionTurretInput(EntityId entityId, BattlefieldPoint position, bool isActivated, bool isDestroyed)
        {
            if (entityId.IsEmpty) throw new ArgumentException("A turret id is required.", nameof(entityId));
            EntityId = entityId;
            Position = position;
            IsActivated = isActivated;
            IsDestroyed = isDestroyed;
        }
    }
}

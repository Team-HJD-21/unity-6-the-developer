// 배치된 방어 시설 하나의 불변 런타임 값입니다.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class TurretState
    {
        public EntityId EntityId { get; }
        public DefinitionId DefinitionId { get; }
        public int Health { get; }
        public bool IsActive { get; }

        public TurretState(EntityId entityId, DefinitionId definitionId, int health, bool isActive)
        {
            if (entityId.IsEmpty) throw new ArgumentException("Turret state requires a valid entity ID.", nameof(entityId));
            if (definitionId.IsEmpty) throw new ArgumentException("Turret state requires a valid definition ID.", nameof(definitionId));
            EntityId = entityId;
            DefinitionId = definitionId;
            Health = health;
            IsActive = isActive;
        }
    }
}

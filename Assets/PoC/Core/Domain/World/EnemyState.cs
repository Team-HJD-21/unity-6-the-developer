// 적 개체 하나의 불변 런타임 값입니다.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class EnemyState
    {
        public EntityId EntityId { get; }
        public DefinitionId ArchetypeId { get; }
        public int Health { get; }
        public bool IsAlive { get; }

        public EnemyState(EntityId entityId, DefinitionId archetypeId, int health, bool isAlive)
        {
            if (entityId.IsEmpty) throw new ArgumentException("Enemy state requires a valid entity ID.", nameof(entityId));
            if (archetypeId.IsEmpty) throw new ArgumentException("Enemy state requires a valid archetype ID.", nameof(archetypeId));
            EntityId = entityId;
            ArchetypeId = archetypeId;
            Health = health;
            IsAlive = isAlive;
        }
    }
}

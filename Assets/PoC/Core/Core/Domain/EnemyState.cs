// Immutable runtime values for one enemy actor.


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
            EntityId = entityId;
            ArchetypeId = archetypeId;
            Health = health;
            IsAlive = isAlive;
        }
    }
}

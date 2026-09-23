// Immutable runtime values for one placed defense unit.


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
            EntityId = entityId;
            DefinitionId = definitionId;
            Health = health;
            IsActive = isActive;
        }
    }
}

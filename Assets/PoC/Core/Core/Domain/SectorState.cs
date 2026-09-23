// Immutable runtime values for one expandable stage sector.


namespace TeamHJD.Game.Domain
{
    public sealed class SectorState
    {
        public DefinitionId SectorDefinitionId { get; }
        public bool IsUnlocked { get; }

        public SectorState(DefinitionId sectorDefinitionId, bool isUnlocked)
        {
            SectorDefinitionId = sectorDefinitionId;
            IsUnlocked = isUnlocked;
        }
    }
}

// Immutable runtime values for one expandable stage sector.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class SectorState
    {
        public DefinitionId SectorDefinitionId { get; }
        public bool IsUnlocked { get; }

        public SectorState(DefinitionId sectorDefinitionId, bool isUnlocked)
        {
            if (sectorDefinitionId.IsEmpty) throw new ArgumentException("Sector state requires a valid definition ID.", nameof(sectorDefinitionId));
            SectorDefinitionId = sectorDefinitionId;
            IsUnlocked = isUnlocked;
        }
    }
}

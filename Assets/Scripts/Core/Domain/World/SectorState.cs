// 확장 가능한 스테이지 구역 하나의 불변 런타임 값입니다.


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

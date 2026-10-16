// Host의 전체 지역 상태와 Match 시작·Grid 세대를 함께 전달합니다.
// GridId는 동등성, Generation은 Reset 순서이며 revision과 서로 대체하지 않습니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class RegionReplicationFrame
    {
        public Guid MatchDescriptorId { get; }
        public long GridGeneration { get; }
        public RegionGridSnapshot Snapshot { get; }
        public RegionReplicationFrame(Guid matchDescriptorId, long gridGeneration, RegionGridSnapshot snapshot)
        {
            if (matchDescriptorId == Guid.Empty) throw new ArgumentException("A descriptor id is required.", nameof(matchDescriptorId));
            if (gridGeneration < 1) throw new ArgumentOutOfRangeException(nameof(gridGeneration));
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            MatchDescriptorId = matchDescriptorId; GridGeneration = gridGeneration;
        }
    }
}

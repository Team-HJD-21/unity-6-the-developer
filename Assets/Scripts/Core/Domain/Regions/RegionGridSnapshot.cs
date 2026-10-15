// 한 Match와 Grid 세대의 확정된 셀 점령 결과입니다.
// 기하 Polygon이나 Unity 객체를 담지 않으며 과거 Snapshot은 변경되지 않습니다.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class RegionGridSnapshot
    {
        public MatchId MatchId { get; }
        public Guid GridId { get; }
        public long Revision { get; }
        public RegionPolicyMode Mode { get; }
        public RegionGridDefinition Definition { get; }
        public IReadOnlyList<bool> CapturedCells { get; }
        public int CapturedCellCount { get; }

        internal RegionGridSnapshot(MatchId matchId, Guid gridId, long revision,
            RegionPolicyMode mode, RegionGridDefinition definition, bool[] capturedCells)
        {
            MatchId = matchId;
            GridId = gridId;
            Revision = revision;
            Mode = mode;
            Definition = definition;
            var copied = (bool[])capturedCells.Clone();
            int count = 0;
            foreach (bool captured in copied) if (captured) count++;
            CapturedCellCount = count;
            CapturedCells = new ReadOnlyCollection<bool>(copied);
        }

        public bool IsCaptured(int x, int y) => CapturedCells[Definition.GetCellIndex(x, y)];

        public bool TryIsCapturedAt(BattlefieldPoint point, out bool isCaptured)
        {
            bool found = Definition.TryGetCellIndex(point, out int index);
            isCaptured = found && CapturedCells[index];
            return found;
        }
    }
}

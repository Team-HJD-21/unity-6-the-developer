// 지역 셀 점령의 변경을 검증하고 새 불변 Snapshot을 확정합니다.
// 잘못된 입력은 전체 변경 전에 거부하며 변경이 없으면 revision을 유지합니다.
using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    public sealed class RegionGridState
    {
        public RegionGridSnapshot Snapshot { get; private set; }

        public RegionGridState(MatchId matchId, Guid gridId, RegionGridDefinition definition,
            RegionPolicyMode mode = RegionPolicyMode.Static)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A match id is required.", nameof(matchId));
            if (gridId == Guid.Empty) throw new ArgumentException("A grid id is required.", nameof(gridId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!Enum.IsDefined(typeof(RegionPolicyMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Snapshot = new RegionGridSnapshot(matchId, gridId, 0, mode, definition,
                new bool[definition.Geometry.TotalCellCount]);
        }

        public bool Apply(IEnumerable<int> capturedCellIndices, RegionCaptureUpdateMode updateMode)
        {
            if (capturedCellIndices == null) throw new ArgumentNullException(nameof(capturedCellIndices));
            if (!Enum.IsDefined(typeof(RegionCaptureUpdateMode), updateMode))
                throw new ArgumentOutOfRangeException(nameof(updateMode));

            RegionGridSnapshot previous = Snapshot;
            var next = new bool[previous.CapturedCells.Count];
            if (updateMode == RegionCaptureUpdateMode.Union)
                for (int index = 0; index < next.Length; index++) next[index] = previous.CapturedCells[index];

            foreach (int index in capturedCellIndices)
            {
                if (index < 0 || index >= next.Length)
                    throw new ArgumentOutOfRangeException(nameof(capturedCellIndices), "A cell is outside this grid.");
                next[index] = true;
            }

            bool changed = false;
            for (int index = 0; index < next.Length; index++)
                if (next[index] != previous.CapturedCells[index]) { changed = true; break; }
            if (!changed) return false;

            long revision = checked(previous.Revision + 1);
            Snapshot = new RegionGridSnapshot(previous.MatchId, previous.GridId, revision,
                previous.Mode, previous.Definition, next);
            return true;
        }
    }
}

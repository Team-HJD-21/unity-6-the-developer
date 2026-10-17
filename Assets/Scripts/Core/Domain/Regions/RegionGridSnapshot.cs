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
            if (matchId.IsEmpty) throw new ArgumentException("A match id is required.", nameof(matchId));
            if (gridId == Guid.Empty) throw new ArgumentException("A grid id is required.", nameof(gridId));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(RegionPolicyMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (capturedCells == null || capturedCells.Length != definition.Geometry.TotalCellCount)
                throw new ArgumentException("Capture cells must match the grid definition.", nameof(capturedCells));
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

        /// <summary>저장/복제 어댑터의 전체 Snapshot을 검증하여 복원합니다. 부분 갱신 명령이 아닙니다.</summary>
        public static RegionGridSnapshot FromCapturedCells(MatchId matchId, Guid gridId, long revision,
            RegionPolicyMode mode, RegionGridDefinition definition, IEnumerable<int> capturedCells)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (capturedCells == null) throw new ArgumentNullException(nameof(capturedCells));
            var cells = new bool[definition.Geometry.TotalCellCount];
            foreach (int index in capturedCells)
            {
                if (index < 0 || index >= cells.Length) throw new ArgumentOutOfRangeException(nameof(capturedCells));
                cells[index] = true;
            }
            return new RegionGridSnapshot(matchId, gridId, revision, mode, definition, cells);
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

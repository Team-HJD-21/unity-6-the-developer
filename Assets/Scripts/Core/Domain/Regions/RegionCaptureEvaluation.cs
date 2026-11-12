// 동적 지역 판정의 셀 결과와 진단 자료입니다. 이 객체 자체는 점령 상태가 아닙니다.
// 삼각형은 시각 진단용이며 실제 소비자는 Runtime에 확정된 Grid를 조회합니다.
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class RegionCaptureEvaluation
    {
        public RegionGridDefinition Definition { get; }
        public IReadOnlyList<int> CellIndices { get; }
        public BattlefieldSpatialSnapshot Geometry { get; }
        public int DuplicatePositionCount { get; }
        public int InitialCellCount { get; }
        public int DisconnectedCellCount { get; }

        internal RegionCaptureEvaluation(RegionGridDefinition definition, List<int> cells,
            BattlefieldSpatialSnapshot geometry, int duplicatePositionCount,
            int initialCellCount = 0, int disconnectedCellCount = 0)
        {
            Definition = definition;
            CellIndices = new ReadOnlyCollection<int>(new List<int>(cells));
            Geometry = geometry;
            DuplicatePositionCount = duplicatePositionCount;
            InitialCellCount = initialCellCount;
            DisconnectedCellCount = disconnectedCellCount;
        }
    }
}

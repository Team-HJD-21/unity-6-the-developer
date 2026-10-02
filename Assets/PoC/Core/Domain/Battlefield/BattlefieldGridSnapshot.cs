// 한 설정 revision에서 계산된 균일 Grid와 셀 점유를 읽기 전용으로 제공합니다.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldGridSnapshot
    {
        private static readonly BattlefieldGridSnapshot EmptySnapshot = CreateEmptySnapshot();

        public BattlefieldGridConfiguration Configuration { get; }
        public IReadOnlyList<BattlefieldGridCellSnapshot> Cells { get; }
        public IReadOnlyList<BattlefieldGridCellSnapshot> OccupiedCells { get; }
        public IReadOnlyList<EntityId> OutOfBoundsTurretIds { get; }
        public IReadOnlyList<EntityId> OutOfBoundsPlayerIds { get; }
        public IReadOnlyList<EntityId> OutOfBoundsEnemyIds { get; }

        internal static BattlefieldGridSnapshot Empty => EmptySnapshot;

        internal BattlefieldGridSnapshot(
            BattlefieldGridConfiguration configuration,
            IList<BattlefieldGridCellSnapshot> cells,
            IList<EntityId> outOfBoundsTurretIds,
            IList<EntityId> outOfBoundsPlayerIds,
            IList<EntityId> outOfBoundsEnemyIds)
        {
            Configuration = configuration;
            var copiedCells = new List<BattlefieldGridCellSnapshot>(cells);
            var occupiedCells = new List<BattlefieldGridCellSnapshot>();
            foreach (var cell in copiedCells)
                if (cell.IsOccupied) occupiedCells.Add(cell);
            Cells = new ReadOnlyCollection<BattlefieldGridCellSnapshot>(copiedCells);
            OccupiedCells = new ReadOnlyCollection<BattlefieldGridCellSnapshot>(occupiedCells);
            OutOfBoundsTurretIds = new ReadOnlyCollection<EntityId>(new List<EntityId>(outOfBoundsTurretIds));
            OutOfBoundsPlayerIds = new ReadOnlyCollection<EntityId>(new List<EntityId>(outOfBoundsPlayerIds));
            OutOfBoundsEnemyIds = new ReadOnlyCollection<EntityId>(new List<EntityId>(outOfBoundsEnemyIds));
        }

        public bool TryGetCellAt(BattlefieldPoint point, out BattlefieldGridCellSnapshot cell)
        {
            if (Configuration.TryMapPoint(point, out var x, out var y))
            {
                cell = Cells[y * Configuration.CellsX + x];
                return true;
            }

            cell = null;
            return false;
        }

        private static BattlefieldGridSnapshot CreateEmptySnapshot() =>
            new BattlefieldGridBuilder().Build(
                BattlefieldGridConfiguration.Default,
                BattlefieldSpatialInput.Empty,
                BattlefieldDynamicSpatialInput.Empty);
    }
}

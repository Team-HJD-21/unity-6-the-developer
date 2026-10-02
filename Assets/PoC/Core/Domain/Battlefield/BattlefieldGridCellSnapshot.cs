// Grid 셀 하나의 안정된 좌표와 원시 점유 ID를 읽기 전용으로 제공합니다.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldGridCellSnapshot
    {
        private static readonly IReadOnlyList<EntityId> EmptyIds = System.Array.Empty<EntityId>();

        public int CellId { get; }
        public int X { get; }
        public int Y { get; }
        public IReadOnlyList<EntityId> TurretIds { get; }
        public IReadOnlyList<EntityId> PlayerIds { get; }
        public IReadOnlyList<EntityId> EnemyIds { get; }
        public int TurretCount => TurretIds.Count;
        public int PlayerCount => PlayerIds.Count;
        public int EnemyCount => EnemyIds.Count;
        public bool IsOccupied => TurretCount > 0 || PlayerCount > 0 || EnemyCount > 0;

        internal BattlefieldGridCellSnapshot(
            int cellId,
            int x,
            int y,
            IList<EntityId> turretIds,
            IList<EntityId> playerIds,
            IList<EntityId> enemyIds)
        {
            CellId = cellId;
            X = x;
            Y = y;
            TurretIds = CopyIds(turretIds);
            PlayerIds = CopyIds(playerIds);
            EnemyIds = CopyIds(enemyIds);
        }

        private static IReadOnlyList<EntityId> CopyIds(IList<EntityId> ids)
        {
            if (ids == null || ids.Count == 0) return EmptyIds;
            return new ReadOnlyCollection<EntityId>(new List<EntityId>(ids));
        }
    }
}

// 명시적 위치 입력을 설정 가능한 XY Uniform Grid snapshot으로 집계합니다.

using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldGridBuilder
    {
        public BattlefieldGridSnapshot Build(
            BattlefieldGridConfiguration configuration,
            BattlefieldSpatialInput staticInput,
            BattlefieldDynamicSpatialInput dynamicInput)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (staticInput == null) throw new ArgumentNullException(nameof(staticInput));
            if (dynamicInput == null) throw new ArgumentNullException(nameof(dynamicInput));

            var turretCells = CreateCellLists(configuration.TotalCellCount);
            var playerCells = CreateCellLists(configuration.TotalCellCount);
            var enemyCells = CreateCellLists(configuration.TotalCellCount);
            var outsideTurrets = new List<EntityId>();
            var outsidePlayers = new List<EntityId>();
            var outsideEnemies = new List<EntityId>();

            foreach (var turret in staticInput.Turrets)
                Add(configuration, turret.Position, turret.EntityId, turretCells, outsideTurrets);
            foreach (var player in dynamicInput.Players)
                Add(configuration, player.Position, player.EntityId, playerCells, outsidePlayers);
            foreach (var enemy in dynamicInput.Enemies)
                Add(configuration, enemy.Position, enemy.EntityId, enemyCells, outsideEnemies);

            var cells = new List<BattlefieldGridCellSnapshot>(configuration.TotalCellCount);
            for (var y = 0; y < configuration.CellsY; y++)
            for (var x = 0; x < configuration.CellsX; x++)
            {
                var cellId = y * configuration.CellsX + x;
                SortIfPresent(turretCells[cellId]);
                SortIfPresent(playerCells[cellId]);
                SortIfPresent(enemyCells[cellId]);
                cells.Add(new BattlefieldGridCellSnapshot(cellId, x, y,
                    turretCells[cellId], playerCells[cellId], enemyCells[cellId]));
            }

            Sort(outsideTurrets);
            Sort(outsidePlayers);
            Sort(outsideEnemies);
            return new BattlefieldGridSnapshot(configuration, cells, outsideTurrets, outsidePlayers, outsideEnemies);
        }

        private static List<EntityId>[] CreateCellLists(int count)
        {
            return new List<EntityId>[count];
        }

        private static void Add(
            BattlefieldGridConfiguration configuration,
            BattlefieldPoint point,
            EntityId entityId,
            List<EntityId>[] cells,
            IList<EntityId> outOfBounds)
        {
            if (!configuration.TryMapPoint(point, out var x, out var y))
            {
                outOfBounds.Add(entityId);
                return;
            }
            var cellId = y * configuration.CellsX + x;
            if (cells[cellId] == null) cells[cellId] = new List<EntityId>();
            cells[cellId].Add(entityId);
        }

        private static void SortIfPresent(List<EntityId> ids)
        {
            if (ids != null) Sort(ids);
        }

        private static void Sort(List<EntityId> ids) => ids.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.Value, right.Value));
    }
}

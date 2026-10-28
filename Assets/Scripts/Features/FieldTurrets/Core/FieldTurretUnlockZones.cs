using System;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    /// <summary>Cell-distance bands around a CU position. Neither CU nor turrets store a CellId.</summary>
    public sealed class FieldTurretUnlockZones
    {
        public FieldTurretGridConfiguration Grid { get; }
        public int CuCellX { get; }
        public int CuCellY { get; }
        public int ZoneCount { get; }
        public int MaximumCellDistance { get; }

        public FieldTurretUnlockZones(FieldTurretGridConfiguration grid, Vector2 cuPosition, int zoneCount)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (zoneCount < 1) throw new ArgumentOutOfRangeException(nameof(zoneCount));
            if (!grid.TryMapPoint(cuPosition, out int x, out int y))
                throw new ArgumentOutOfRangeException(nameof(cuPosition), "CU is outside the turret unlock grid.");
            CuCellX = x;
            CuCellY = y;
            MaximumCellDistance = Math.Max(x, grid.CellsX - 1 - x) +
                                  Math.Max(y, grid.CellsY - 1 - y);
            ZoneCount = Math.Min(zoneCount, MaximumCellDistance + 1);
        }

        public int GetZoneForCell(int x, int y)
        {
            if (x < 0 || x >= Grid.CellsX || y < 0 || y >= Grid.CellsY)
                return 0;
            int distance = Math.Abs(x - CuCellX) + Math.Abs(y - CuCellY);
            return Math.Min(ZoneCount, (int)((long)distance * ZoneCount /
                (MaximumCellDistance + 1)) + 1);
        }

        public bool TryGetZone(Vector2 position, out int zone)
        {
            if (!Grid.TryMapPoint(position, out int x, out int y))
            {
                zone = 0;
                return false;
            }
            zone = GetZoneForCell(x, y);
            return true;
        }
    }
}

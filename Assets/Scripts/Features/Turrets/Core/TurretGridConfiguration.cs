using System;
using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    /// <summary>Turret-owned bounds and uniform cell divisions for regional unlocks.</summary>
    public sealed class TurretGridConfiguration
    {
        public const float DefaultMapWidth = 275f;
        public const float DefaultMapHeight = 275f;
        public const int DefaultCellsX = 16;
        public const int DefaultCellsY = 16;

        public Vector2 Origin { get; }
        public float MinimumX => Origin.x - MapWidth * 0.5f;
        public float MaximumX => Origin.x + MapWidth * 0.5f;
        public float MinimumY => Origin.y - MapHeight * 0.5f;
        public float MaximumY => Origin.y + MapHeight * 0.5f;
        public float OriginZ { get; }
        public float MapWidth { get; }
        public float MapHeight { get; }
        public int CellsX { get; }
        public int CellsY { get; }
        public float CellWidth => MapWidth / CellsX;
        public float CellHeight => MapHeight / CellsY;

        public TurretGridConfiguration(
            Vector2 origin, float mapWidth, float mapHeight, int cellsX, int cellsY, float originZ = 0f)
        {
            if (!IsFinite(origin.x)) throw new ArgumentOutOfRangeException(nameof(origin));
            if (!IsFinite(origin.y)) throw new ArgumentOutOfRangeException(nameof(origin));
            if (!IsFinite(mapWidth) || mapWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            if (!IsFinite(mapHeight) || mapHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(mapHeight));
            if (!IsFinite(originZ)) throw new ArgumentOutOfRangeException(nameof(originZ));
            if (cellsX <= 0) throw new ArgumentOutOfRangeException(nameof(cellsX));
            if (cellsY <= 0) throw new ArgumentOutOfRangeException(nameof(cellsY));
            if ((long)cellsX * cellsY > 262144)
                throw new ArgumentOutOfRangeException(nameof(cellsY), "Turret grid cannot exceed 262144 cells.");

            Origin = origin;
            OriginZ = originZ;
            MapWidth = mapWidth;
            MapHeight = mapHeight;
            CellsX = cellsX;
            CellsY = cellsY;
        }

        public bool TryMapPoint(Vector2 point, out int cellX, out int cellY)
        {
            if (point.x < MinimumX || point.x > MaximumX || point.y < MinimumY || point.y > MaximumY)
            {
                cellX = -1;
                cellY = -1;
                return false;
            }

            cellX = point.x == MaximumX ? CellsX - 1 : Mathf.FloorToInt((point.x - MinimumX) / CellWidth);
            cellY = point.y == MaximumY ? CellsY - 1 : Mathf.FloorToInt((point.y - MinimumY) / CellHeight);
            return cellX >= 0 && cellX < CellsX && cellY >= 0 && cellY < CellsY;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

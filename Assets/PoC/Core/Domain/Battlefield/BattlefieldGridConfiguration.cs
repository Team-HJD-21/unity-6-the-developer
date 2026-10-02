// Unity 2D XY 전장을 나누는 불변 설정입니다.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldGridConfiguration
    {
        public const int DefaultCellsX = 16;
        public const int DefaultCellsY = 16;
        public const int MaximumCellCount = 262144;

        public BattlefieldPoint Origin { get; }
        public double OriginZ { get; }
        public double MapWidth { get; }
        public double MapHeight { get; }
        public int CellsX { get; }
        public int CellsY { get; }
        public double CellWidth => MapWidth / CellsX;
        public double CellHeight => MapHeight / CellsY;
        public int TotalCellCount => CellsX * CellsY;

        public static BattlefieldGridConfiguration Default =>
            new BattlefieldGridConfiguration(new BattlefieldPoint(0d, 0d), 0d, 16d, 16d, DefaultCellsX, DefaultCellsY);

        public BattlefieldGridConfiguration(
            BattlefieldPoint origin,
            double originZ,
            double mapWidth,
            double mapHeight,
            int cellsX,
            int cellsY)
        {
            if (double.IsNaN(originZ) || double.IsInfinity(originZ)) throw new ArgumentOutOfRangeException(nameof(originZ));
            if (double.IsNaN(mapWidth) || double.IsInfinity(mapWidth) || mapWidth <= 0d) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            if (double.IsNaN(mapHeight) || double.IsInfinity(mapHeight) || mapHeight <= 0d) throw new ArgumentOutOfRangeException(nameof(mapHeight));
            if (cellsX <= 0) throw new ArgumentOutOfRangeException(nameof(cellsX));
            if (cellsY <= 0) throw new ArgumentOutOfRangeException(nameof(cellsY));
            if ((long)cellsX * cellsY > MaximumCellCount)
                throw new ArgumentOutOfRangeException(nameof(cellsY), $"Grid cannot exceed {MaximumCellCount} cells.");
            if (double.IsInfinity(origin.X + mapWidth) || double.IsInfinity(origin.Y + mapHeight))
                throw new ArgumentOutOfRangeException(nameof(mapWidth), "Grid maximum bounds must remain finite.");
            if (mapWidth / cellsX <= 0d || mapHeight / cellsY <= 0d)
                throw new ArgumentOutOfRangeException(nameof(cellsX), "Grid cell dimensions must remain greater than zero.");

            Origin = origin;
            OriginZ = originZ;
            MapWidth = mapWidth;
            MapHeight = mapHeight;
            CellsX = cellsX;
            CellsY = cellsY;
        }

        public bool TryMapPoint(BattlefieldPoint point, out int cellX, out int cellY)
        {
            var maxX = Origin.X + MapWidth;
            var maxY = Origin.Y + MapHeight;
            if (point.X < Origin.X || point.X > maxX || point.Y < Origin.Y || point.Y > maxY)
            {
                cellX = -1;
                cellY = -1;
                return false;
            }

            cellX = point.X == maxX ? CellsX - 1 : (int)Math.Floor((point.X - Origin.X) / CellWidth);
            cellY = point.Y == maxY ? CellsY - 1 : (int)Math.Floor((point.Y - Origin.Y) / CellHeight);
            return cellX >= 0 && cellX < CellsX && cellY >= 0 && cellY < CellsY;
        }
    }
}

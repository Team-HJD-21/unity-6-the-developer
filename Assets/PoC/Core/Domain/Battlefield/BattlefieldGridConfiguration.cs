// Unity 2D XY 전장을 나누는 불변 설정입니다. Origin은 Grid의 중심 좌표입니다.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldGridConfiguration
    {
        public const int DefaultCellsX = 16;
        public const int DefaultCellsY = 16;
        public const int MaximumCellCount = 262144;

        public BattlefieldPoint Origin { get; }
        public double MinimumX => Origin.X - MapWidth * 0.5d;
        public double MaximumX => Origin.X + MapWidth * 0.5d;
        public double MinimumY => Origin.Y - MapHeight * 0.5d;
        public double MaximumY => Origin.Y + MapHeight * 0.5d;
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
            if (double.IsNaN(origin.X) || double.IsInfinity(origin.X)) throw new ArgumentOutOfRangeException(nameof(origin));
            if (double.IsNaN(origin.Y) || double.IsInfinity(origin.Y)) throw new ArgumentOutOfRangeException(nameof(origin));
            if (double.IsNaN(originZ) || double.IsInfinity(originZ)) throw new ArgumentOutOfRangeException(nameof(originZ));
            if (double.IsNaN(mapWidth) || double.IsInfinity(mapWidth) || mapWidth <= 0d) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            if (double.IsNaN(mapHeight) || double.IsInfinity(mapHeight) || mapHeight <= 0d) throw new ArgumentOutOfRangeException(nameof(mapHeight));
            if (cellsX <= 0) throw new ArgumentOutOfRangeException(nameof(cellsX));
            if (cellsY <= 0) throw new ArgumentOutOfRangeException(nameof(cellsY));
            if ((long)cellsX * cellsY > MaximumCellCount)
                throw new ArgumentOutOfRangeException(nameof(cellsY), $"Grid cannot exceed {MaximumCellCount} cells.");
            if (double.IsInfinity(origin.X - mapWidth * 0.5d) || double.IsInfinity(origin.X + mapWidth * 0.5d) ||
                double.IsInfinity(origin.Y - mapHeight * 0.5d) || double.IsInfinity(origin.Y + mapHeight * 0.5d))
                throw new ArgumentOutOfRangeException(nameof(mapWidth), "Grid bounds must remain finite around its center.");
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
            if (point.X < MinimumX || point.X > MaximumX || point.Y < MinimumY || point.Y > MaximumY)
            {
                cellX = -1;
                cellY = -1;
                return false;
            }

            cellX = point.X == MaximumX ? CellsX - 1 : (int)Math.Floor((point.X - MinimumX) / CellWidth);
            cellY = point.Y == MaximumY ? CellsY - 1 : (int)Math.Floor((point.Y - MinimumY) / CellHeight);
            return cellX >= 0 && cellX < CellsX && cellY >= 0 && cellY < CellsY;
        }
    }
}

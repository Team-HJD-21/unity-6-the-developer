// 삼각형을 Grid 셀에 투영하고 양의 교차 면적이 있는 셀만 반환합니다.
// 셀 단위 정규화로 오차를 제한하며 경계선·점 접촉은 점령으로 취급하지 않습니다.
using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    public sealed class TriangleGridRasterizer
    {
        // 셀 넓이에 대한 수치 오차 한계입니다. 게임 밸런스용 점령 비율이 아닙니다.
        public const double MinimumAreaFraction = 1e-10;

        public IReadOnlyList<int> Rasterize(BattlefieldPoint a, BattlefieldPoint b, BattlefieldPoint c,
            BattlefieldGridConfiguration grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            var polygon = new List<BattlefieldPoint> { Normalize(a, grid), Normalize(b, grid), Normalize(c, grid) };
            polygon = Clip(polygon, true, 0, true);
            polygon = Clip(polygon, true, grid.CellsX, false);
            polygon = Clip(polygon, false, 0, true);
            polygon = Clip(polygon, false, grid.CellsY, false);
            var cells = new List<int>();
            if (polygon.Count < 3) return cells.AsReadOnly();

            double minX = grid.CellsX, minY = grid.CellsY, maxX = 0, maxY = 0;
            foreach (BattlefieldPoint point in polygon)
            {
                minX = Math.Min(minX, point.X); minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X); maxY = Math.Max(maxY, point.Y);
            }
            int startX = Math.Max(0, (int)Math.Floor(minX));
            int startY = Math.Max(0, (int)Math.Floor(minY));
            int endX = Math.Min(grid.CellsX - 1, (int)Math.Ceiling(maxX) - 1);
            int endY = Math.Min(grid.CellsY - 1, (int)Math.Ceiling(maxY) - 1);
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    List<BattlefieldPoint> cell = Clip(polygon, true, x, true);
                    cell = Clip(cell, true, x + 1, false);
                    cell = Clip(cell, false, y, true);
                    cell = Clip(cell, false, y + 1, false);
                    if (Area(cell) > MinimumAreaFraction) cells.Add(y * grid.CellsX + x);
                }
            }
            return cells.AsReadOnly();
        }

        private static BattlefieldPoint Normalize(BattlefieldPoint point, BattlefieldGridConfiguration grid)
        {
            return new BattlefieldPoint(
                ((point.X - grid.Origin.X) / grid.MapWidth + 0.5d) * grid.CellsX,
                ((point.Y - grid.Origin.Y) / grid.MapHeight + 0.5d) * grid.CellsY);
        }

        private static List<BattlefieldPoint> Clip(List<BattlefieldPoint> source, bool xAxis,
            double limit, bool keepGreater)
        {
            var result = new List<BattlefieldPoint>(source.Count + 1);
            if (source.Count == 0) return result;
            BattlefieldPoint previous = source[source.Count - 1];
            double previousValue = xAxis ? previous.X : previous.Y;
            bool previousInside = keepGreater ? previousValue >= limit : previousValue <= limit;
            foreach (BattlefieldPoint current in source)
            {
                double currentValue = xAxis ? current.X : current.Y;
                bool currentInside = keepGreater ? currentValue >= limit : currentValue <= limit;
                if (currentInside != previousInside)
                {
                    double t = (limit - previousValue) / (currentValue - previousValue);
                    result.Add(xAxis
                        ? new BattlefieldPoint(limit, previous.Y + (current.Y - previous.Y) * t)
                        : new BattlefieldPoint(previous.X + (current.X - previous.X) * t, limit));
                }
                if (currentInside) result.Add(current);
                previous = current;
                previousValue = currentValue;
                previousInside = currentInside;
            }
            return result;
        }

        private static double Area(List<BattlefieldPoint> points)
        {
            if (points.Count < 3) return 0;
            // 셀 근처의 원점으로 옮겨 큰 좌표의 shoelace 상쇄 오차를 줄입니다.
            BattlefieldPoint origin = points[0];
            double twiceArea = 0;
            for (int index = 1; index < points.Count - 1; index++)
                twiceArea += (points[index].X - origin.X) * (points[index + 1].Y - origin.Y)
                    - (points[index].Y - origin.Y) * (points[index + 1].X - origin.X);
            return Math.Abs(twiceArea) * 0.5d;
        }
    }
}

// 일반 스폰의 구역·점유 경계 제한을 같은 Region Snapshot에서 판정합니다.
// 정적 확보 구역은 그룹 전체를 제외하고, 나머지는 스폰 반경과 여유 거리로 검사합니다.
// 예산이나 점유 상태를 변경하지 않으며 Planner와 실제 생성 직전에 함께 사용합니다.
using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    public sealed class RegionSpawnPolicy
    {
        private readonly RegionGridSnapshot _snapshot;
        private readonly HashSet<int> _capturedRegions = new HashSet<int>();
        private readonly double _margin;

        public RegionSpawnPolicy(RegionGridSnapshot snapshot, double margin = 0)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            RequireNonNegativeFinite(margin, nameof(margin));
            _margin = margin;
            if (snapshot.Mode != RegionPolicyMode.Static) return;

            var incomplete = new HashSet<int>();
            for (int i = 0; i < snapshot.Definition.RegionIds.Count; i++)
            {
                int region = snapshot.Definition.RegionIds[i];
                if (region == 0 || !snapshot.Definition.CapturableCells[i]) continue;
                _capturedRegions.Add(region);
                if (!snapshot.CapturedCells[i]) incomplete.Add(region);
            }
            _capturedRegions.ExceptWith(incomplete);
        }

        /// <summary>Collector/Planner가 할당 전에 정적 Region 그룹 전체의 제외 여부를 읽습니다.</summary>
        /// <remarks>해금 또는 CU 한 셀의 점유만으로 구역 전체를 확보했다고 판단하지 않습니다.</remarks>
        public bool IsRegionCaptured(int regionId) => _capturedRegions.Contains(regionId);

        /// <summary>일반 스폰 후보와 Executor가 동일하게 호출합니다. 거리는 월드 XY 단위입니다.</summary>
        /// <returns>가능하면 빈 문자열, 불가하면 진단용 제외 사유를 반환합니다.</returns>
        public string GetExclusionReason(BattlefieldPoint position, double spawnRadius)
        {
            RequireNonNegativeFinite(spawnRadius, nameof(spawnRadius));
            var definition = _snapshot.Definition;
            if (!definition.TryGetCellIndex(position, out int cell)) return "OutsideGrid";
            if (!definition.CapturableCells[cell]) return "NonCapturableCell";
            if (IsRegionCaptured(definition.RegionIds[cell])) return "CapturedRegion";
            if (_snapshot.CapturedCellCount == 0) return string.Empty;

            double radius = spawnRadius + _margin;
            if (double.IsInfinity(radius)) return "CapturedBoundary";
            var grid = definition.Geometry;
            int x0 = ToCell(Math.Max(grid.MinimumX, position.X - radius), grid.MinimumX, grid.CellWidth, grid.CellsX);
            int x1 = ToCell(Math.Min(grid.MaximumX, position.X + radius), grid.MinimumX, grid.CellWidth, grid.CellsX);
            int y0 = ToCell(Math.Max(grid.MinimumY, position.Y - radius), grid.MinimumY, grid.CellHeight, grid.CellsY);
            int y1 = ToCell(Math.Min(grid.MaximumY, position.Y + radius), grid.MinimumY, grid.CellHeight, grid.CellsY);

            // 접선도 차단하는 규칙이므로, 정확히 셀 경계에서 끝나면 반대쪽 셀도 검사합니다.
            x0 = Math.Max(0, x0 - 1);
            y0 = Math.Max(0, y0 - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!_snapshot.IsCaptured(x, y)) continue;
                    double left = grid.MinimumX + x * grid.CellWidth;
                    double bottom = grid.MinimumY + y * grid.CellHeight;
                    double dx = Math.Max(left - position.X, Math.Max(0, position.X - (left + grid.CellWidth)));
                    double dy = Math.Max(bottom - position.Y, Math.Max(0, position.Y - (bottom + grid.CellHeight)));
                    if (Touches(dx, dy, radius)) return "CapturedBoundary";
                }
            return string.Empty;
        }

        private static int ToCell(double value, double minimum, double size, int count)
            => Math.Max(0, Math.Min(count - 1, (int)Math.Floor((value - minimum) / size)));

        private static bool Touches(double dx, double dy, double radius)
        {
            if (dx > radius || dy > radius) return false;
            if (radius == 0) return dx == 0 && dy == 0;
            // 큰 좌표에서도 제곱 오버플로 없이 원과 셀 사각형의 접촉을 판단합니다.
            double nx = dx / radius, ny = dy / radius;
            return nx * nx + ny * ny <= 1;
        }

        private static void RequireNonNegativeFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}

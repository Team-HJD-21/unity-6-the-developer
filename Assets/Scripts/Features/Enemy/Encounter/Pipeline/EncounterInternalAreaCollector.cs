// 내부 추가 출현에 필요한 실제 NavMesh 영역을 제한된 수만 수집합니다.
// 점유 Grid 안쪽 경계·현재 객체/Player 거리·실제 이동면을 대조하고 Scene 생성은 하지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.AI;

namespace TeamHJD.Game.Enemies.Encounter
{
    internal static class EncounterInternalAreaCollector
    {
        public static IReadOnlyList<SpawnArea> Collect(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region,
            TargetableComponent[] targets, double playerDistance)
        {
            var grid = battlefield.Grid.Configuration;
            var result = new List<SpawnArea>();
            var geometry = region.Definition.Geometry;
            if (!geometry.Origin.Equals(grid.Origin) || geometry.MapWidth != grid.MapWidth || geometry.MapHeight != grid.MapHeight ||
                geometry.CellsX != grid.CellsX || geometry.CellsY != grid.CellsY) return result;
            var players = targets.Where(t => t != null && t.TargetType == TargetType.Player && t.CanBeTargeted)
                .Select(t => t.transform.position).ToArray();
            // 전체 Grid가 커도 가장 한산한 경계 후보64개까지만 이동면을 검사합니다.
            var candidates = Enumerable.Range(0, region.CapturedCells.Count)
                .Where(i => region.CapturedCells[i] && region.Definition.CapturableCells[i] &&
                    battlefield.Grid.Cells[i].PlayerCount == 0 && battlefield.Grid.Cells[i].TurretCount == 0 && IsBoundary(i))
                .OrderBy(i => battlefield.Grid.Cells[i].EnemyCount).ThenBy(i => i).Take(64);
            foreach (int index in candidates)
            {
                int x = index % grid.CellsX, y = index / grid.CellsX;
                double left = grid.MinimumX + x * grid.CellWidth, bottom = grid.MinimumY + y * grid.CellHeight;
                double centerX = left + grid.CellWidth / 2, centerY = bottom + grid.CellHeight / 2;
                if (Math.Abs(centerX) > float.MaxValue || Math.Abs(centerY) > float.MaxValue) continue;
                var center = new Vector3((float)centerX, (float)centerY, 0);
                if (!NavMesh.SamplePosition(center, out var hit, .5f, NavMesh.AllAreas)) continue;
                if (!grid.TryMapPoint(new BattlefieldPoint(hit.position.x, hit.position.y), out int hx, out int hy) ||
                    hy * grid.CellsX + hx != index) continue;
                double halfX = Math.Min(3, Math.Min(hit.position.x - left, left + grid.CellWidth - hit.position.x)) * .9;
                double halfY = Math.Min(3, Math.Min(hit.position.y - bottom, bottom + grid.CellHeight - hit.position.y)) * .9;
                if (halfX <= .1 || halfY <= .1) continue;
                double radius = Math.Sqrt(halfX * halfX + halfY * halfY);
                if (players.Any(p => Vector2.Distance(p, hit.position) < playerDistance + radius)) continue;
                result.Add(new SpawnArea(new BattlefieldPoint(hit.position.x, hit.position.y), halfX * 2, halfY * 2, grid.OriginZ));
            }
            return result.AsReadOnly();

            bool IsBoundary(int cell)
            {
                int x = cell % grid.CellsX, y = cell / grid.CellsX;
                return Outside(x - 1, y) || Outside(x + 1, y) || Outside(x, y - 1) || Outside(x, y + 1);
            }
            bool Outside(int x, int y) => x >= 0 && x < grid.CellsX && y >= 0 && y < grid.CellsY &&
                region.Definition.CapturableCells[y * grid.CellsX + x] && !region.CapturedCells[y * grid.CellsX + x];
        }
    }
}

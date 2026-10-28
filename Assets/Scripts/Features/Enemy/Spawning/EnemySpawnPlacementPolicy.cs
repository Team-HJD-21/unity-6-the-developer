// World origin의 XY 후보를 요청 영역·현재 Grid·NavMesh 표면과 대조합니다.
// 적마다 최대 16회만 탐색하며 실패를 보고하고 다른 위치/전달 방식으로 대체하지 않습니다.
using System;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.AI;
public static class EnemySpawnPlacementPolicy
{
    public static bool TryResolve(SpawnArea area, BattlefieldSpatialSnapshot battlefield, System.Random random,
        out Vector3 position, out string reason)
    {
        position = default; reason = "No valid location inside the requested area/grid/NavMesh after 16 attempts.";
        if (area == null || battlefield == null || random == null || Math.Abs(area.VisualZ) > float.MaxValue) return false;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            double x = area.Center.X + (random.NextDouble() - .5) * area.Width;
            double y = area.Center.Y + (random.NextDouble() - .5) * area.Height;
            if (Math.Abs(x) > float.MaxValue || Math.Abs(y) > float.MaxValue) continue;
            // 기존 Enemy 길찾기와 동일한 Unity 2D XY NavMesh 표현을 사용합니다.
            if (!NavMesh.SamplePosition(new Vector3((float)x, (float)y, 0), out var hit, .5f, NavMesh.AllAreas)) continue;
            if (Math.Abs(hit.position.x - area.Center.X) > area.Width / 2 + .001 ||
                Math.Abs(hit.position.y - area.Center.Y) > area.Height / 2 + .001) continue;
            var xy = new BattlefieldPoint(hit.position.x, hit.position.y);
            if (!battlefield.Grid.TryGetCellAt(xy, out var cell) || cell.PlayerCount > 0 || cell.TurretCount > 0) continue;
            position = new Vector3(hit.position.x, hit.position.y, (float)area.VisualZ); reason = ""; return true;
        }
        return false;
    }
}

// 지도와 Field가 함께 사용하는 확정 점유 셀의 월드 XY 외곽선입니다.
// 맞닿은 셀의 공유 변은 제거하며, 구멍과 분리된 점유지도 각각 닫힌 경계로 반환합니다.
// 점유 판단이나 터렛 전선은 변경하지 않고 RegionGridSnapshot만 읽습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using UnityEngine;

public readonly struct RegionBoundarySegment
{
    public Vector2 Start { get; }
    public Vector2 End { get; }
    /// <summary>선을 Start→End로 따라갈 때 점유 측인 왼쪽을 가리킵니다.</summary>
    public Vector2 Inward { get; }

    public RegionBoundarySegment(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
        var direction = (end - start).normalized;
        Inward = new Vector2(-direction.y, direction.x);
    }
}

public static class RegionBoundaryGeometry
{
    private readonly struct GridEdge
    {
        public readonly Vector2Int Start, End;
        public GridEdge(Vector2Int start, Vector2Int end) { Start = start; End = end; }
    }

    /// <summary>지도에서 미점유 유효 지형의 점유 가능/잠김 경계만 표시합니다.</summary>
    public static IReadOnlyList<RegionBoundarySegment> BuildUnlockBoundary(RegionGridSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Mode != RegionPolicyMode.Static)
            return Array.Empty<RegionBoundarySegment>();
        var definition = snapshot.Definition;
        var geometry = definition.Geometry;
        var edges = new List<RegionBoundarySegment>();

        bool IsUnlockEdge(int first, int second)
        {
            // 이미 점유된 셀의 변은 공용 점유 외곽선이 표시합니다.
            if (snapshot.CapturedCells[first] || snapshot.CapturedCells[second] ||
                !definition.CapturableCells[first] || !definition.CapturableCells[second]) return false;
            return snapshot.IsRegionUnlocked(definition.RegionIds[first]) !=
                snapshot.IsRegionUnlocked(definition.RegionIds[second]);
        }

        for (int y = 0; y < geometry.CellsY; y++)
        for (int x = 0; x < geometry.CellsX; x++)
        {
            int index = definition.GetCellIndex(x, y);
            if (x + 1 < geometry.CellsX && IsUnlockEdge(index, definition.GetCellIndex(x + 1, y)))
                edges.Add(new RegionBoundarySegment(ToWorld(geometry, new Vector2Int(x + 1, y)),
                    ToWorld(geometry, new Vector2Int(x + 1, y + 1))));
            if (y + 1 < geometry.CellsY && IsUnlockEdge(index, definition.GetCellIndex(x, y + 1)))
                edges.Add(new RegionBoundarySegment(ToWorld(geometry, new Vector2Int(x, y + 1)),
                    ToWorld(geometry, new Vector2Int(x + 1, y + 1))));
        }
        return edges.AsReadOnly();
    }

    /// <summary>지도/바닥 표시가 같은 Snapshot에서 호출합니다. 각 닫힌 경계는 연속된 선분으로 반환됩니다.</summary>
    public static IReadOnlyList<RegionBoundarySegment> Build(RegionGridSnapshot snapshot)
    {
        if (snapshot == null) return Array.Empty<RegionBoundarySegment>();
        var grid = snapshot.Definition.Geometry;
        var edges = new List<GridEdge>();
        for (int y = 0; y < grid.CellsY; y++)
        for (int x = 0; x < grid.CellsX; x++)
        {
            if (!snapshot.IsCaptured(x, y)) continue;
            if (y == 0 || !snapshot.IsCaptured(x, y - 1)) edges.Add(new GridEdge(new(x, y), new(x + 1, y)));
            if (x == grid.CellsX - 1 || !snapshot.IsCaptured(x + 1, y)) edges.Add(new GridEdge(new(x + 1, y), new(x + 1, y + 1)));
            if (y == grid.CellsY - 1 || !snapshot.IsCaptured(x, y + 1)) edges.Add(new GridEdge(new(x + 1, y + 1), new(x, y + 1)));
            if (x == 0 || !snapshot.IsCaptured(x - 1, y)) edges.Add(new GridEdge(new(x, y + 1), new(x, y)));
        }

        var outgoing = new Dictionary<Vector2Int, List<int>>();
        for (int i = 0; i < edges.Count; i++)
        {
            if (!outgoing.TryGetValue(edges[i].Start, out var next)) outgoing[edges[i].Start] = next = new List<int>(2);
            next.Add(i);
        }
        var used = new bool[edges.Count];
        var result = new List<RegionBoundarySegment>(edges.Count);
        for (int first = 0; first < edges.Count; first++)
        {
            if (used[first]) continue;
            int index = first;
            do
            {
                var edge = edges[index];
                used[index] = true;
                result.Add(new RegionBoundarySegment(ToWorld(grid, edge.Start), ToWorld(grid, edge.End)));
                if (edge.End == edges[first].Start) break;
                int chosen = -1, bestTurn = int.MinValue;
                var direction = edge.End - edge.Start;
                foreach (int candidate in outgoing[edge.End])
                {
                    if (used[candidate]) continue;
                    var next = edges[candidate].End - edges[candidate].Start;
                    int turn = direction.x * next.y - direction.y * next.x;
                    // 대각선으로만 닿은 점유 셀은 왼쪽 회전을 우선해 별도 경계로 유지합니다.
                    if (turn > bestTurn) { bestTurn = turn; chosen = candidate; }
                }
                if (chosen < 0) throw new InvalidOperationException("The captured-cell boundary must be closed.");
                index = chosen;
            } while (true);
        }
        return result.AsReadOnly();
    }

    private static Vector2 ToWorld(BattlefieldGridConfiguration grid, Vector2Int point) =>
        new((float)(grid.MinimumX + point.x * grid.CellWidth), (float)(grid.MinimumY + point.y * grid.CellHeight));
}

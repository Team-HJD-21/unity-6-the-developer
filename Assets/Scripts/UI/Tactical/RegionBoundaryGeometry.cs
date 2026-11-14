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
        Vector2 World(Vector2Int p) => new((float)(grid.MinimumX + p.x * grid.CellWidth),
            (float)(grid.MinimumY + p.y * grid.CellHeight));

        for (int first = 0; first < edges.Count; first++)
        {
            if (used[first]) continue;
            int index = first;
            do
            {
                var edge = edges[index];
                used[index] = true;
                result.Add(new RegionBoundarySegment(World(edge.Start), World(edge.End)));
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
}

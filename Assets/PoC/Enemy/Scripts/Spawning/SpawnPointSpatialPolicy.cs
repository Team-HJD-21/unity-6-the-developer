// Battlefield Grid와 전선으로 SpawnPoint 후보 및 분대 세트 배분을 결정합니다.
// Unity 오브젝트와 생성 권한을 소유하지 않는 Stage 1 임시 Encounter 정책입니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;

public sealed class SpawnPointSpatialPolicy
{
    private sealed class CandidateState
    {
        public string Id;
        public int EnemyCount;
        public double FrontlineDistanceSquared;
        public int AssignedSets;
    }

    public IReadOnlyList<SpatialSpawnAllocation> Allocate(
        BattlefieldSpatialSnapshot snapshot,
        IReadOnlyList<SpatialSpawnCandidate> candidates,
        int totalSets,
        int enemiesPerSet)
    {
        var allocations = new List<SpatialSpawnAllocation>();
        if (snapshot?.Grid == null || candidates == null || totalSets <= 0 || enemiesPerSet <= 0)
            return allocations;

        var vertices = new Dictionary<EntityId, BattlefieldPoint>();
        foreach (BattlefieldVertex vertex in snapshot.Vertices)
            vertices[vertex.EntityId] = vertex.Position;

        var eligible = new List<CandidateState>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpatialSpawnCandidate candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Id) || !seenIds.Add(candidate.Id) ||
                !snapshot.Grid.TryGetCellAt(candidate.Position, out BattlefieldGridCellSnapshot cell) ||
                cell.TurretCount > 0 || cell.PlayerCount > 0)
                continue;

            eligible.Add(new CandidateState
            {
                Id = candidate.Id,
                EnemyCount = cell.EnemyCount,
                FrontlineDistanceSquared = DistanceToFrontlineSquared(candidate.Position, snapshot.FrontlineEdges, vertices)
            });
        }

        // 기존 적과 이번 요청의 배정량을 함께 계산해 한 지점에 몰리지 않도록 한다.
        for (int set = 0; set < totalSets && eligible.Count > 0; set++)
        {
            CandidateState best = eligible[0];
            for (int index = 1; index < eligible.Count; index++)
            {
                CandidateState next = eligible[index];
                int nextPressure = next.EnemyCount + next.AssignedSets * enemiesPerSet;
                int bestPressure = best.EnemyCount + best.AssignedSets * enemiesPerSet;
                if (nextPressure < bestPressure ||
                    nextPressure == bestPressure &&
                    (next.FrontlineDistanceSquared < best.FrontlineDistanceSquared ||
                     next.FrontlineDistanceSquared == best.FrontlineDistanceSquared &&
                     StringComparer.Ordinal.Compare(next.Id, best.Id) < 0))
                    best = next;
            }
            best.AssignedSets++;
        }

        eligible.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        foreach (CandidateState candidate in eligible)
            if (candidate.AssignedSets > 0)
                allocations.Add(new SpatialSpawnAllocation(candidate.Id, candidate.AssignedSets));
        return allocations.AsReadOnly();
    }

    private static double DistanceToFrontlineSquared(
        BattlefieldPoint point,
        IReadOnlyList<BattlefieldEdge> edges,
        IReadOnlyDictionary<EntityId, BattlefieldPoint> vertices)
    {
        if (edges.Count == 0) return 0d;
        double best = double.MaxValue;
        foreach (BattlefieldEdge edge in edges)
        {
            if (!vertices.TryGetValue(edge.First, out BattlefieldPoint first) ||
                !vertices.TryGetValue(edge.Second, out BattlefieldPoint second))
                continue;

            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            double lengthSquared = dx * dx + dy * dy;
            double projection = lengthSquared > 0d
                ? ((point.X - first.X) * dx + (point.Y - first.Y) * dy) / lengthSquared
                : 0d;
            double t = Math.Max(0d, Math.Min(1d, projection));
            double x = first.X + t * dx - point.X;
            double y = first.Y + t * dy - point.Y;
            best = Math.Min(best, x * x + y * y);
        }
        return best;
    }
}

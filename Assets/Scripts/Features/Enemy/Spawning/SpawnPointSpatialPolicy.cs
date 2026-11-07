// Battlefield Grid와 전선으로 SpawnPoint 후보 및 분대 세트 배분을 결정합니다.
// 셀 공유 부하와 Match별 실제 생성 이력으로 반복 편중을 방지합니다.
// Unity 오브젝트와 생성 권한을 소유하지 않는 Encounter 정책입니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;

public sealed class SpawnPointSpatialPolicy
{
    public const string Description = "Grid cell service + current enemies + pending enemies → lower current pressure → point service → frontline distance → ordinal ID; actual spawns committed; complete sets only";
    private readonly SpawnDistributionHistory _history = new();

    public const string AutomaticDescription = "Per-point context + weighted preset selection; shared request and allocation budgets; cell capacity and pending enemies; Core random; complete squads only";

    public SpawnPointPlanningSession BeginPlanning(BattlefieldSpatialSnapshot snapshot,
        IReadOnlyList<SpatialSpawnCandidate> candidates, SquadSelectionSettings settings)
    {
        if (settings != null && !settings.IsValid)
            throw new ArgumentException("Invalid squad selection settings.", nameof(settings));
        return new SpawnPointPlanningSession(snapshot, candidates, _history, settings);
    }

    public IReadOnlyList<SpatialSpawnAllocation> Allocate(
        BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpatialSpawnCandidate> candidates,
        int totalSets, int enemiesPerSet)
        => Allocate(snapshot, candidates, totalSets, enemiesPerSet, out _);

    public IReadOnlyList<SpatialSpawnAllocation> Allocate(
        BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpatialSpawnCandidate> candidates,
        int totalSets, int enemiesPerSet, out IReadOnlyList<SpawnDecisionSnapshot.Candidate> observations)
    {
        observations = Array.Empty<SpawnDecisionSnapshot.Candidate>();
        if (snapshot?.Grid == null || candidates == null || totalSets <= 0 || enemiesPerSet <= 0)
            return Array.Empty<SpatialSpawnAllocation>();

        // 고정 편성 경로는 기존처럼 셀 수용 상한 없이 동일한 세트 크기를 배분한다.
        var session = BeginPlanning(snapshot, candidates, null);
        for (int set = 0; set < totalSets; set++)
        {
            if (!session.TryGetNextPoint(_ => enemiesPerSet, out SpatialSpawnCandidate point)) break;
            session.Reserve(point, enemiesPerSet);
        }
        observations = session.GetObservations();
        return session.GetAllocations();
    }

    /// <summary>실제 생성만 이력에 반영합니다. 실패한 생성이나 미실행 계획은 부하를 소비하지 않습니다.</summary>
    public void RecordExecuted(BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpawnDecisionSnapshot.Actual> actuals)
    {
        if (!_history.Matches(snapshot) || actuals == null) return;
        foreach (SpawnDecisionSnapshot.Actual actual in actuals) _history.RecordSpawn(actual.PointId);
    }

    internal static double DistanceToFrontlineSquared(
        BattlefieldPoint point,
        IReadOnlyList<BattlefieldEdge> edges,
        IReadOnlyDictionary<EntityId, BattlefieldPoint> vertices)
    {
        if (edges.Count == 0) return double.PositiveInfinity;
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

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
    internal SpawnDistributionHistory.State CaptureCheckpoint() => _history.CaptureCheckpoint();
    internal void RestoreCheckpoint(SpawnDistributionHistory.State state, MatchId match) => _history.RestoreCheckpoint(state, match);


    private sealed class CellState
    {
        public int Key, EnemyCount;
        public long Service, AssignedEnemies;
        public CandidateState BestPoint;
        public long Score => Service + EnemyCount + AssignedEnemies;
    }

    private sealed class CandidateState
    {
        public string Id;
        public CellState Cell;
        public long PointService;
        public double FrontlineDistanceSquared;
        public int AssignedSets;
        public SpatialSpawnCandidate Input;
        public int CellX, CellY;
    }

    public IReadOnlyList<SpatialSpawnAllocation> Allocate(
        BattlefieldSpatialSnapshot snapshot,
        IReadOnlyList<SpatialSpawnCandidate> candidates,
        int totalSets,
        int enemiesPerSet)
        => Allocate(snapshot, candidates, totalSets, enemiesPerSet, out _);

    public IReadOnlyList<SpatialSpawnAllocation> Allocate(
        BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpatialSpawnCandidate> candidates,
        int totalSets, int enemiesPerSet, out IReadOnlyList<SpawnDecisionSnapshot.Candidate> observations)
    {
        var decisions = new List<SpawnDecisionSnapshot.Candidate>();
        observations = decisions.AsReadOnly();
        var allocations = new List<SpatialSpawnAllocation>();
        if (snapshot?.Grid == null || candidates == null || totalSets <= 0 || enemiesPerSet <= 0)
            return allocations;

        var vertices = new Dictionary<EntityId, BattlefieldPoint>();
        foreach (BattlefieldVertex vertex in snapshot.Vertices)
            vertices[vertex.EntityId] = vertex.Position;

        var eligible = new List<CandidateState>();
        var cells = new Dictionary<int, CellState>();
        var pointCells = new Dictionary<string, int>(StringComparer.Ordinal);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpatialSpawnCandidate candidate in candidates)
        {
            bool mapped = snapshot.Grid.TryGetCellAt(candidate.Position, out BattlefieldGridCellSnapshot cell);
            string reason = string.IsNullOrWhiteSpace(candidate.Id) ? "Missing ID" :
                !seenIds.Add(candidate.Id) ? "Duplicate ID" : !mapped ? "Outside grid" :
                cell.TurretCount > 0 ? "Turret occupied cell" : cell.PlayerCount > 0 ? "Player occupied cell" : "";
            if (reason.Length > 0)
            {
                decisions.Add(new SpawnDecisionSnapshot.Candidate(candidate.Id,candidate.Position,cell?.X??-1,cell?.Y??-1,
                    cell?.EnemyCount??0,0,0,false,reason));
                continue;
            }

            int key = cell.Y * snapshot.Grid.Configuration.CellsX + cell.X;
            if (!cells.TryGetValue(key, out CellState state))
                cells.Add(key, state = new CellState { Key = key, EnemyCount = cell.EnemyCount });
            pointCells.Add(candidate.Id, key);
            eligible.Add(new CandidateState
            {
                Id = candidate.Id,
                Cell = state,
                Input = candidate, CellX = cell.X, CellY = cell.Y,
                FrontlineDistanceSquared = DistanceToFrontlineSquared(candidate.Position, snapshot.FrontlineEdges, vertices)
            });
        }

        _history.Prepare(snapshot, pointCells);
        foreach (CellState cell in cells.Values) cell.Service = _history.CellService(cell.Key);
        foreach (CandidateState candidate in eligible) candidate.PointService = _history.PointService(candidate.Id);

        // 같은 셀의 지점들은 예정량을 공유합니다. 거리 우선으로 매 요청 같은 지점을 고르지 않습니다.
        for (int set = 0; set < totalSets && eligible.Count > 0; set++)
        {
            foreach (CellState cell in cells.Values) cell.BestPoint = null;
            foreach (CandidateState candidate in eligible)
            {
                CandidateState current = candidate.Cell.BestPoint;
                if (current == null || ComparePoint(candidate, current, enemiesPerSet) < 0)
                    candidate.Cell.BestPoint = candidate;
            }
            CandidateState best = null;
            foreach (CellState cell in cells.Values)
                if (best == null || CompareCell(cell.BestPoint, best) < 0) best = cell.BestPoint;
            best.AssignedSets++;
            best.Cell.AssignedEnemies += enemiesPerSet;
        }

        eligible.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        foreach (CandidateState candidate in eligible)
        {
            decisions.Add(new SpawnDecisionSnapshot.Candidate(candidate.Id,candidate.Input.Position,candidate.CellX,candidate.CellY,
                candidate.Cell.EnemyCount,candidate.AssignedSets,Math.Sqrt(candidate.FrontlineDistanceSquared),
                snapshot.FrontlineEdges.Count>0,candidate.AssignedSets>0 ? "Allocated by cell service / pressure / point service / frontline" : "Eligible; budget exhausted",
                candidate.Cell.Service,candidate.PointService,candidate.Cell.AssignedEnemies,candidate.Cell.Score,true));
            if (candidate.AssignedSets > 0)
                allocations.Add(new SpatialSpawnAllocation(candidate.Id, candidate.AssignedSets));
        }
        return allocations.AsReadOnly();
    }

    /// <summary>실제 생성만 이력에 반영합니다. 실패한 생성이나 미실행 계획은 부하를 소비하지 않습니다.</summary>
    public void RecordExecuted(BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpawnDecisionSnapshot.Actual> actuals)
    {
        if (!_history.Matches(snapshot) || actuals == null) return;
        foreach (SpawnDecisionSnapshot.Actual actual in actuals) _history.RecordSpawn(actual.PointId, actual.Position);
    }

    private static int CompareCell(CandidateState left, CandidateState right)
    {
        int order = left.Cell.Score.CompareTo(right.Cell.Score);
        if (order != 0) return order;
        order = (left.Cell.EnemyCount + left.Cell.AssignedEnemies).CompareTo(right.Cell.EnemyCount + right.Cell.AssignedEnemies);
        if (order != 0) return order;
        // 셀 간에는 총 부하와 전선 거리만 비교합니다. 복제된 지점 수는 셀의 가중치가 아닙니다.
        order = left.FrontlineDistanceSquared.CompareTo(right.FrontlineDistanceSquared);
        return order != 0 ? order : StringComparer.Ordinal.Compare(left.Id, right.Id);
    }

    private static int ComparePoint(CandidateState left, CandidateState right, int enemiesPerSet)
    {
        int order = (left.PointService + (long)left.AssignedSets * enemiesPerSet).CompareTo(right.PointService + (long)right.AssignedSets * enemiesPerSet);
        if (order != 0) return order;
        order = left.FrontlineDistanceSquared.CompareTo(right.FrontlineDistanceSquared);
        return order != 0 ? order : StringComparer.Ordinal.Compare(left.Id, right.Id);
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

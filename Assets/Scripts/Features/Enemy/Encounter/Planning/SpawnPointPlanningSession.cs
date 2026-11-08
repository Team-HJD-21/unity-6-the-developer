using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

/// <summary>요청 한 번의 셀·지점별 예정 병력을 보관한다. 실제 생성 이력은 변경하지 않는다.</summary>
public sealed class SpawnPointPlanningSession
{
    private sealed class CellState
    {
        public int Key, X, Y, EnemyCount;
        public long Service, AssignedEnemies;
        public long Score => Service + EnemyCount + AssignedEnemies;
    }

    private sealed class AreaBaseline
    {
        public long Enemies;
        public bool HasNearbyPlayer;
    }

    private sealed class PointState
    {
        public SpatialSpawnCandidate Input;
        public CellState Cell;
        public long Service, AssignedEnemies;
        public int AssignedSets;
        public double FrontlineDistanceSquared;
    }

    private readonly List<PointState> _points = new();
    private readonly Dictionary<string, PointState> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<int, CellState> _cells = new();
    private readonly List<SpawnDecisionSnapshot.Candidate> _rejected = new();
    private readonly int _cellLimit;
    private readonly BattlefieldGridSnapshot _grid;
    private readonly int _nearbyCellRadius;
    private readonly Dictionary<int, AreaBaseline> _targetAreas = new();

    internal SpawnPointPlanningSession(BattlefieldSpatialSnapshot snapshot,
        IReadOnlyList<SpatialSpawnCandidate> candidates, SpawnDistributionHistory history,
        SquadSelectionSettings settings)
    {
        if (snapshot?.Grid == null) throw new ArgumentNullException(nameof(snapshot));
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        _grid = snapshot.Grid;
        _nearbyCellRadius = settings?.nearbyCellRadius ?? 0;
        _cellLimit = settings == null ? int.MaxValue : settings.maxEnemiesPerCell;
        var vertices = new Dictionary<EntityId, BattlefieldPoint>();
        foreach (BattlefieldVertex vertex in snapshot.Vertices)
            vertices[vertex.EntityId] = vertex.Position;

        var pointCells = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpatialSpawnCandidate point in candidates)
        {
            bool mapped = snapshot.Grid.TryGetCellAt(point.Position, out BattlefieldGridCellSnapshot cell);
            string reason = string.IsNullOrWhiteSpace(point.Id) ? "Missing ID" :
                !seen.Add(point.Id) ? "Duplicate ID" : !mapped ? "Outside grid" :
                cell.TurretCount > 0 ? "Turret occupied cell" :
                cell.PlayerCount > 0 ? "Player occupied cell" : "";
            if (reason.Length > 0)
            {
                _rejected.Add(new SpawnDecisionSnapshot.Candidate(point.Id, point.Position,
                    cell?.X ?? -1, cell?.Y ?? -1, cell?.EnemyCount ?? 0, 0, 0, false, reason));
                continue;
            }

            int key = cell.CellId;
            if (!_cells.TryGetValue(key, out CellState state))
            {
                state = new CellState { Key = key, X = cell.X, Y = cell.Y, EnemyCount = cell.EnemyCount };
                _cells.Add(key, state);
            }
            var candidate = new PointState
            {
                Input = point, Cell = state,
                FrontlineDistanceSquared = SpawnPointSpatialPolicy.DistanceToFrontlineSquared(
                    point.Position, snapshot.FrontlineEdges, vertices)
            };
            _points.Add(candidate);
            _byId.Add(point.Id, candidate);
            pointCells.Add(point.Id, key);
        }

        history.Prepare(snapshot, pointCells);
        foreach (CellState cell in _cells.Values) cell.Service = history.CellService(cell.Key);
        foreach (PointState point in _points) point.Service = history.PointService(point.Input.Id);
        _points.Sort((left, right) => StringComparer.Ordinal.Compare(left.Input.Id, right.Input.Id));
    }

    /// <summary>예산에 맞는 최소 편성이 들어갈 수 있는 지점 중 부하가 낮은 곳을 선택한다.</summary>
    public bool TryGetNextPoint(Func<SpatialSpawnCandidate, int> getMinimumCount, out SpatialSpawnCandidate point)
    {
        point = default;
        if (getMinimumCount == null) return false;
        var bestPerCell = new Dictionary<int, PointState>();
        foreach (PointState candidate in _points)
        {
            int minimum = getMinimumCount(candidate.Input);
            if (minimum <= 0 || minimum > (long)_cellLimit - candidate.Cell.EnemyCount - candidate.Cell.AssignedEnemies)
                continue;
            if (!bestPerCell.TryGetValue(candidate.Cell.Key, out PointState current) ||
                ComparePoint(candidate, current) < 0)
                bestPerCell[candidate.Cell.Key] = candidate;
        }
        PointState best = null;
        foreach (PointState candidate in bestPerCell.Values)
            if (best == null || CompareCell(candidate, best) < 0) best = candidate;
        if (best == null) return false;
        point = best.Input;
        return true;
    }

    /// <summary>선택한 타깃 주변의 현재 상황과 출현 셀의 예정 수를 반환한다.</summary>
    public SpawnAreaContext GetAreaContext(SpatialSpawnCandidate point, BattlefieldPoint targetPosition,
        int totalEnemyCount, IReadOnlyList<BattlefieldPoint> playerPositions,
        TargetAttackType attackType = TargetAttackType.Unknown)
    {
        PointState state = _byId[point.Id];
        if (!_grid.TryGetCellAt(targetPosition, out BattlefieldGridCellSnapshot targetCell))
            throw new ArgumentException("집계 대상이 Battlefield Grid 밖에 있습니다.", nameof(targetPosition));

        // 같은 타깃 셀을 향하는 스폰 지점은 요청 안에서 주변 집계 결과를 재사용한다.
        if (!_targetAreas.TryGetValue(targetCell.CellId, out AreaBaseline area))
        {
            area = ReadArea(_grid, targetCell.X, targetCell.Y, _nearbyCellRadius, playerPositions);
            _targetAreas.Add(targetCell.CellId, area);
        }

        return new SpawnAreaContext(
            ToCount(state.Cell.EnemyCount + state.Cell.AssignedEnemies),
            ToCount(area.Enemies), totalEnemyCount, area.HasNearbyPlayer, attackType);
    }

    /// <summary>생성 가능한 지점을 타깃 셀로 묶고 최소 편성 비용과 기존 생성 이력을 집계한다.</summary>
    public IReadOnlyList<SpawnBudgetPolicy.Area> GetBudgetAreas(
        IReadOnlyDictionary<string, BattlefieldPoint> targetPositions, int totalEnemyCount,
        IReadOnlyList<BattlefieldPoint> playerPositions, Func<int, int> getMinimumCost)
    {
        var groups = new Dictionary<int, List<string>>();
        var minimumCosts = new Dictionary<int, int>();
        var previousCounts = new Dictionary<int, long>();
        foreach (PointState point in _points)
        {
            if (!targetPositions.TryGetValue(point.Input.Id, out BattlefieldPoint target) ||
                !_grid.TryGetCellAt(target, out BattlefieldGridCellSnapshot targetCell))
                continue;

            long capacity = (long)_cellLimit - point.Cell.EnemyCount - point.Cell.AssignedEnemies;
            if (capacity <= 0) continue;
            int minimumCost = getMinimumCost((int)capacity);
            if (minimumCost <= 0) continue;

            int key = targetCell.CellId;
            if (!groups.TryGetValue(key, out List<string> ids))
            {
                groups.Add(key, ids = new List<string>());
                minimumCosts.Add(key, minimumCost);
                previousCounts.Add(key, 0);
            }
            ids.Add(point.Input.Id);
            minimumCosts[key] = Math.Min(minimumCosts[key], minimumCost);
            previousCounts[key] = checked(previousCounts[key] + point.Service);
        }

        var areas = new List<SpawnBudgetPolicy.Area>(groups.Count);
        foreach (var group in groups)
        {
            SpatialSpawnCandidate first = _byId[group.Value[0]].Input;
            SpawnAreaContext situation = GetAreaContext(first, targetPositions[first.Id],
                totalEnemyCount, playerPositions);
            areas.Add(new SpawnBudgetPolicy.Area($"target-cell-{group.Key}", group.Value,
                situation, minimumCosts[group.Key], previousCounts[group.Key]));
        }
        return areas.AsReadOnly();
    }

    /// <summary>출현 셀을 공유하는 모든 지점의 용량 검사에 한 편성의 예정 수량을 반영한다.</summary>
    public void Reserve(SpatialSpawnCandidate point, int count)
    {
        PointState state = _byId[point.Id];
        if (count <= 0 || count > (long)_cellLimit - state.Cell.EnemyCount - state.Cell.AssignedEnemies)
            throw new ArgumentOutOfRangeException(nameof(count));
        state.AssignedSets++;
        state.AssignedEnemies += count;
        state.Cell.AssignedEnemies += count;
    }

    /// <summary>
    /// 이번 계획에서 하나 이상의 편성 세트를 배정받은 지점과 세트 수를 반환한다.
    /// </summary>
    public IReadOnlyList<SpatialSpawnAllocation> GetAllocations()
    {
        var allocations = new List<SpatialSpawnAllocation>();
        foreach (PointState point in _points)
            if (point.AssignedSets > 0)
                allocations.Add(new SpatialSpawnAllocation(point.Input.Id, point.AssignedSets));
        return allocations.AsReadOnly();
    }

    /// <summary>
    /// 배정 결과와 후보 제외 이유를 실행 전 진단 자료로 반환한다.
    /// </summary>
    public IReadOnlyList<SpawnDecisionSnapshot.Candidate> GetObservations()
    {
        var result = new List<SpawnDecisionSnapshot.Candidate>(_rejected);
        foreach (PointState point in _points)
            result.Add(new SpawnDecisionSnapshot.Candidate(point.Input.Id, point.Input.Position,
                point.Cell.X, point.Cell.Y, point.Cell.EnemyCount, point.AssignedSets,
                double.IsInfinity(point.FrontlineDistanceSquared) ? 0d : Math.Sqrt(point.FrontlineDistanceSquared),
                !double.IsInfinity(point.FrontlineDistanceSquared),
                point.AssignedSets > 0 ? "Allocated by cell pressure / point service / frontline" :
                "Eligible; no remaining budget, capacity or affordable preset",
                point.Cell.Service, point.Service, point.Cell.AssignedEnemies, point.Cell.Score, true));
        return result.AsReadOnly();
    }

    private static int ComparePoint(PointState left, PointState right)
    {
        int order = (left.Service + left.AssignedEnemies).CompareTo(right.Service + right.AssignedEnemies);
        if (order != 0) return order;
        order = left.FrontlineDistanceSquared.CompareTo(right.FrontlineDistanceSquared);
        return order != 0 ? order : StringComparer.Ordinal.Compare(left.Input.Id, right.Input.Id);
    }

    private static int CompareCell(PointState left, PointState right)
    {
        int order = left.Cell.Score.CompareTo(right.Cell.Score);
        if (order != 0) return order;
        order = (left.Cell.EnemyCount + left.Cell.AssignedEnemies).CompareTo(right.Cell.EnemyCount + right.Cell.AssignedEnemies);
        if (order != 0) return order;
        order = left.FrontlineDistanceSquared.CompareTo(right.FrontlineDistanceSquared);
        return order != 0 ? order : StringComparer.Ordinal.Compare(left.Input.Id, right.Input.Id);
    }

    /// <summary>
    /// 타깃 셀 주변의 존재하는 셀만 읽어 병력을 합산하고 유효한 플레이어 위치의 포함 여부를 확인한다.
    /// </summary>
    private static AreaBaseline ReadArea(BattlefieldGridSnapshot grid, int x, int y, int radius,
        IReadOnlyList<BattlefieldPoint> playerPositions)
    {
        var result = new AreaBaseline();
        int width = grid.Configuration.CellsX;
        int height = grid.Configuration.CellsY;
        int minX = (int)Math.Max(0L, (long)x - radius);
        int maxX = (int)Math.Min(width - 1L, (long)x + radius);
        int minY = (int)Math.Max(0L, (long)y - radius);
        int maxY = (int)Math.Min(height - 1L, (long)y + radius);
        for (int cellY = minY; cellY <= maxY; cellY++)
        for (int cellX = minX; cellX <= maxX; cellX++)
        {
            int key = cellY * width + cellX;
            BattlefieldGridCellSnapshot cell = grid.Cells[key];
            result.Enemies += cell.EnemyCount;
        }
        foreach (BattlefieldPoint position in playerPositions)
        {
            if (!grid.TryGetCellAt(position, out BattlefieldGridCellSnapshot playerCell))
                continue;
            if (playerCell.X < minX || playerCell.X > maxX ||
                playerCell.Y < minY || playerCell.Y > maxY)
                continue;

            result.HasNearbyPlayer = true;
            break;
        }
        return result;
    }

    private static int ToCount(long value) => (int)Math.Min(int.MaxValue, value);
}

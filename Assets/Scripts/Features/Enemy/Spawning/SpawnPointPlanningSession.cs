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
        public long Enemies, Turrets;
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
        int totalEnemyCount, IReadOnlyList<BattlefieldPoint> playerPositions)
    {
        PointState state = _byId[point.Id];
        if (!_grid.TryGetCellAt(targetPosition, out BattlefieldGridCellSnapshot targetCell))
            throw new ArgumentException("집계 대상이 Battlefield Grid 밖에 있습니다.", nameof(targetPosition));

        if (!_targetAreas.TryGetValue(targetCell.CellId, out AreaBaseline area))
        {
            area = ReadArea(_grid, targetCell.X, targetCell.Y, _nearbyCellRadius, playerPositions);
            _targetAreas.Add(targetCell.CellId, area);
        }

        bool hasFrontline = !double.IsInfinity(state.FrontlineDistanceSquared);
        return new SpawnAreaContext(
            ToCount(state.Cell.EnemyCount + state.Cell.AssignedEnemies),
            ToCount(area.Enemies), ToCount(area.Turrets), hasFrontline,
            hasFrontline ? Math.Sqrt(state.FrontlineDistanceSquared) : 0d, totalEnemyCount, area.HasNearbyPlayer);
    }

    /// <summary>같은 셀의 모든 지점과 주변 조사에 한 편성의 예정 수량을 반영한다.</summary>
    public void Reserve(SpatialSpawnCandidate point, int count)
    {
        PointState state = _byId[point.Id];
        if (count <= 0 || count > (long)_cellLimit - state.Cell.EnemyCount - state.Cell.AssignedEnemies)
            throw new ArgumentOutOfRangeException(nameof(count));
        state.AssignedSets++;
        state.AssignedEnemies += count;
        state.Cell.AssignedEnemies += count;
    }

    public IReadOnlyList<SpatialSpawnAllocation> GetAllocations()
    {
        var allocations = new List<SpatialSpawnAllocation>();
        foreach (PointState point in _points)
            if (point.AssignedSets > 0)
                allocations.Add(new SpatialSpawnAllocation(point.Input.Id, point.AssignedSets));
        return allocations.AsReadOnly();
    }

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
            result.Turrets += cell.TurretCount;
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

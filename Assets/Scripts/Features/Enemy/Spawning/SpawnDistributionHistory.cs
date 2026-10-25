// Match별 셀/지점의 생성 서비스 부하를 보관합니다.
// 계획이 아니라 실제 생성만 반영하며 새 Match 또는 XY Grid 변경 시 초기화합니다.
// 신규/재활성 후보는 현재 최소 부하부터 시작해 과거 미참여 기간의 몰아주기를 막습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;

public sealed class SpawnDistributionHistory
{
    private MatchId _matchId;
    private BattlefieldGridConfiguration _grid;
    private readonly Dictionary<int, long> _cells = new();
    private readonly Dictionary<string, long> _points = new(StringComparer.Ordinal);
    private Dictionary<string, int> _pointCells = new(StringComparer.Ordinal);

    public long CellService(int cell) => _cells.TryGetValue(cell, out long value) ? value : 0;
    public long PointService(string point) => _points.TryGetValue(point, out long value) ? value : 0;

    public bool Matches(BattlefieldSpatialSnapshot snapshot) => snapshot?.Grid != null && _grid != null &&
        _matchId == snapshot.MatchId && _grid.Origin.Equals(snapshot.Grid.Configuration.Origin) &&
        _grid.MapWidth == snapshot.Grid.Configuration.MapWidth && _grid.MapHeight == snapshot.Grid.Configuration.MapHeight &&
        _grid.CellsX == snapshot.Grid.Configuration.CellsX && _grid.CellsY == snapshot.Grid.Configuration.CellsY;

    public void Prepare(BattlefieldSpatialSnapshot snapshot, Dictionary<string, int> pointCells)
    {
        if (!Matches(snapshot))
        {
            _cells.Clear(); _points.Clear(); _pointCells.Clear();
            _matchId = snapshot.MatchId; _grid = snapshot.Grid.Configuration;
        }

        var activeCells = new HashSet<int>(pointCells.Values);
        long baseline = long.MaxValue;
        foreach (int cell in activeCells)
            if (_cells.TryGetValue(cell, out long value)) baseline = Math.Min(baseline, value);
        if (baseline == long.MaxValue) baseline = 0;
        foreach (int cell in new List<int>(_cells.Keys))
            if (!activeCells.Contains(cell)) _cells.Remove(cell);
        foreach (string point in new List<string>(_points.Keys))
            if (!pointCells.TryGetValue(point, out int cell) || !_pointCells.TryGetValue(point, out int previous) || cell != previous)
                _points.Remove(point);

        var pointBaselines = new Dictionary<int, long>();
        foreach (var pair in pointCells)
            if (_points.TryGetValue(pair.Key, out long value))
                pointBaselines[pair.Value] = pointBaselines.TryGetValue(pair.Value, out long current) ? Math.Min(current, value) : value;
        foreach (var pair in pointCells)
        {
            if (!_cells.ContainsKey(pair.Value)) _cells.Add(pair.Value, baseline);
            if (!_points.ContainsKey(pair.Key)) _points.Add(pair.Key, pointBaselines.TryGetValue(pair.Value, out long value) ? value : 0);
        }
        _pointCells = new Dictionary<string, int>(pointCells, StringComparer.Ordinal);
    }

    public void RecordSpawn(string pointId)
    {
        if (!_pointCells.TryGetValue(pointId, out int cell)) return;
        _cells[cell] = checked(_cells[cell] + 1);
        _points[pointId] = checked(_points[pointId] + 1);
    }

    public void RecordSpawn(string pointId, BattlefieldPoint actualPosition)
    {
        if (!string.IsNullOrEmpty(pointId)) { RecordSpawn(pointId); return; }
        if (_grid == null || !_grid.TryMapPoint(actualPosition, out int x, out int y)) return;
        int cell = y * _grid.CellsX + x;
        if (_cells.TryGetValue(cell, out long service)) _cells[cell] = checked(service + 1);
    }
}

// 실제 생성 분산 이력을 값으로 보존해 복원 직후 같은 지점에 다시 몰리지 않게 합니다.
// 이전 Match 실행 ID는 가져오지 않고 새 Match와 저장 Grid에 다시 연결합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

public sealed partial class SpawnDistributionHistory
{
    internal State CaptureCheckpoint() => new(_grid, new(_cells), new(_points, StringComparer.Ordinal), new(_pointCells, StringComparer.Ordinal));
    internal void RestoreCheckpoint(State state, MatchId match)
    {
        _grid = state.Grid; _matchId = match; _cells.Clear(); _points.Clear();
        foreach (var pair in state.Cells) _cells.Add(pair.Key, pair.Value);
        foreach (var pair in state.Points) _points.Add(pair.Key, pair.Value);
        _pointCells = new(state.PointCells, StringComparer.Ordinal);
    }

    internal sealed class State
    {
        public readonly BattlefieldGridConfiguration Grid;
        public readonly Dictionary<int, long> Cells;
        public readonly Dictionary<string, long> Points;
        public readonly Dictionary<string, int> PointCells;
        public State(BattlefieldGridConfiguration grid, Dictionary<int, long> cells, Dictionary<string, long> points, Dictionary<string, int> pointCells)
        {
            if (cells.Count > 262144 || points.Count > 16384 || points.Count != pointCells.Count ||
                (grid == null && (cells.Count != 0 || points.Count != 0)) ||
                cells.Any(p => p.Key < 0 || p.Key >= grid.TotalCellCount || p.Value < 0) ||
                points.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0 || !pointCells.TryGetValue(p.Key, out int cell) || !cells.ContainsKey(cell)))
                throw new InvalidDataException("Invalid saved spawn distribution history.");
            Grid = grid; Cells = cells; Points = points; PointCells = pointCells;
        }
        public void Write(BinaryWriter w, ICheckpointPayloadCodec codec)
        {
            w.Write(Grid != null);
            if (Grid != null)
            {
                w.Write(Grid.Origin.X); w.Write(Grid.Origin.Y); w.Write(Grid.OriginZ); w.Write(Grid.MapWidth); w.Write(Grid.MapHeight); w.Write(Grid.CellsX); w.Write(Grid.CellsY);
            }
            w.Write(Cells.Count); foreach (var p in Cells.OrderBy(p => p.Key)) { w.Write(p.Key); w.Write(p.Value); }
            w.Write(Points.Count);
            foreach (var p in Points.OrderBy(p => p.Key, StringComparer.Ordinal)) { codec.WriteText(w, p.Key); w.Write(p.Value); w.Write(PointCells[p.Key]); }
        }
        public static State Read(BinaryReader r, ICheckpointPayloadCodec codec)
        {
            var grid = r.ReadBoolean() ? new BattlefieldGridConfiguration(new BattlefieldPoint(r.ReadDouble(), r.ReadDouble()), r.ReadDouble(),
                r.ReadDouble(), r.ReadDouble(), r.ReadInt32(), r.ReadInt32()) : null;
            var cells = new Dictionary<int, long>(); int count = codec.ReadCount(r, 262144);
            for (int i = 0; i < count; i++) if (!cells.TryAdd(r.ReadInt32(), r.ReadInt64())) throw new InvalidDataException("Duplicate service cell.");
            var points = new Dictionary<string, long>(StringComparer.Ordinal); var pointCells = new Dictionary<string, int>(StringComparer.Ordinal);
            count = codec.ReadCount(r, 16384);
            for (int i = 0; i < count; i++)
            {
                string key = codec.ReadText(r); long service = r.ReadInt64(); int cell = r.ReadInt32();
                if (!points.TryAdd(key, service)) throw new InvalidDataException("Duplicate service point.");
                pointCells.Add(key, cell);
            }
            return new(grid, cells, points, pointCells);
        }
    }
}

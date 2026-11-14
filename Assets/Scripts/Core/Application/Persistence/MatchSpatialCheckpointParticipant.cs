// 실제 Match의 공간 설정과 지역 점유 원본을 저장합니다.
// 삼각형·밀도는 저장하지 않고 실제 개체 복원이 끝난 뒤 조립부에서 다시 계산합니다.
using System;
using System.IO;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchSpatialCheckpointParticipant : ICheckpointParticipant
    {
        private readonly MatchSession _session;
        private readonly Action<RegionGridDefinition, RegionPolicyMode, int[], int[]> _restoreRegions;
        public string Key => "world/spatial";
        public int Version => 2;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 10;

        /// <summary>Scene 조립부가 현재 Session과 지역 바인딩의 명시 복원 함수를 연결합니다.</summary>
        public MatchSpatialCheckpointParticipant(MatchSession session,
            Action<RegionGridDefinition, RegionPolicyMode, int[], int[]> restoreRegions)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _restoreRegions = restoreRegions ?? throw new ArgumentNullException(nameof(restoreRegions));
        }

        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            RequireMatch(context);
            State state = CaptureState();
            return codec.Write(Key, Version, Kind, writer => Write(writer, state));
        }

        public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context,
            CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            RequireMatch(context.Current);
            if (section.Version < 1 || section.Version > Version) throw new InvalidDataException("Unsupported spatial checkpoint version.");
            State target = codec.Read(section, section.Version, reader => Read(reader, codec, section.Version));
            if (section.Version == 1) target = MigrateLegacy(target);
            State backup = CaptureState();
            return new CheckpointRestoreStep(() => Apply(target), () => Apply(backup));
        }

        private void RequireMatch(CheckpointCaptureContext context)
        {
            if (context == null || _session.IsDisposed || context.Config.MatchId != _session.Config.MatchId)
                throw new InvalidOperationException("Use the spatial writer only in its current Match.");
        }

        private State CaptureState()
        {
            RegionGridSnapshot regions = _session.Regions?.Snapshot ??
                throw new InvalidOperationException("Bind the actual region source before capture.");
            int[] captured = Enumerable.Range(0, regions.CapturedCells.Count)
                .Where(index => regions.CapturedCells[index]).ToArray();
            return new State(_session.Battlefield.Grid.Configuration, regions.Definition, regions.Mode, captured, regions.UnlockedRegionIds.ToArray());
        }

        private void Apply(State state)
        {
            _session.ReconfigureBattlefieldGrid(state.Battlefield);
            _restoreRegions(state.Regions, state.Mode, (int[])state.Captured.Clone(), (int[])state.Unlocked.Clone());
        }

        private static void Write(BinaryWriter writer, State state)
        {
            WriteGrid(writer, state.Battlefield);
            WriteGrid(writer, state.Regions.Geometry);
            writer.Write((int)state.Mode);
            writer.Write(state.Regions.RegionIds.Count);
            foreach (int id in state.Regions.RegionIds) writer.Write(id);
            foreach (bool eligible in state.Regions.CapturableCells) writer.Write(eligible);
            writer.Write(state.Unlocked.Length);
            foreach (int id in state.Unlocked) writer.Write(id);
            writer.Write(state.Captured.Length);
            foreach (int index in state.Captured) writer.Write(index);
        }

        private static State Read(BinaryReader reader, ICheckpointPayloadCodec codec, int version)
        {
            try
            {
                BattlefieldGridConfiguration battlefield = ReadGrid(reader), geometry = ReadGrid(reader);
                var mode = (RegionPolicyMode)reader.ReadInt32();
                if (!Enum.IsDefined(typeof(RegionPolicyMode), mode)) throw new InvalidDataException("Unknown region policy.");
                int count = codec.ReadCount(reader, BattlefieldGridConfiguration.MaximumCellCount);
                if (count != geometry.TotalCellCount) throw new InvalidDataException("Region membership does not match its geometry.");
                var ids = new int[count];
                for (int i = 0; i < count; i++) ids[i] = reader.ReadInt32();
                bool[] eligible = null;
                int[] unlocked = Array.Empty<int>();
                if (version >= 2)
                {
                    eligible = new bool[count];
                    for (int i = 0; i < count; i++)
                    {
                        byte value = reader.ReadByte();
                        if (value > 1) throw new InvalidDataException("Invalid eligible cell flag.");
                        eligible[i] = value == 1;
                    }
                    unlocked = new int[codec.ReadCount(reader, count)];
                    for (int i = 0; i < unlocked.Length; i++)
                    {
                        unlocked[i] = reader.ReadInt32();
                        if (unlocked[i] <= 0 || (i > 0 && unlocked[i] <= unlocked[i - 1]) || !ids.Contains(unlocked[i]))
                            throw new InvalidDataException("Invalid or duplicate unlocked region.");
                    }
                }
                var definition = new RegionGridDefinition(geometry, ids, eligible);
                var captured = new int[codec.ReadCount(reader, count)];
                int previous = -1;
                for (int i = 0; i < captured.Length; i++)
                {
                    int index = reader.ReadInt32();
                    if (index <= previous || index >= count) throw new InvalidDataException("Invalid or duplicate captured cell.");
                    captured[i] = index;
                    previous = index;
                }
                if (captured.Any(index => !definition.CapturableCells[index])) throw new InvalidDataException("Captured cell is not eligible.");
                return new State(battlefield, definition, mode, captured, unlocked);
            }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid saved spatial configuration.", error); }
        }

        private State MigrateLegacy(State legacy)
        {
            var current = _session.Regions.Snapshot.Definition;
            var a = legacy.Regions.Geometry; var b = current.Geometry;
            if (a.Origin != b.Origin || a.OriginZ != b.OriginZ || a.MapWidth != b.MapWidth || a.MapHeight != b.MapHeight ||
                a.CellsX != b.CellsX || a.CellsY != b.CellsY)
                throw new InvalidDataException("Legacy spatial save requires the current Stage grid geometry for migration.");
            var definition = new RegionGridDefinition(a, legacy.Regions.RegionIds, current.CapturableCells);
            // v1 정적 셀은 해금 표시였습니다. 이를 전투 확보 완료로 둔갑시키지 않습니다. CU 1셀은 Scene이 보장합니다.
            var unlocked = legacy.Mode == RegionPolicyMode.Static ? legacy.Captured.Select(i => definition.RegionIds[i])
                .Where(id => id > 0).Distinct().OrderBy(id => id).ToArray() : Array.Empty<int>();
            var captured = legacy.Mode == RegionPolicyMode.Static ? Array.Empty<int>() :
                legacy.Captured.Where(i => definition.CapturableCells[i]).ToArray();
            return new State(legacy.Battlefield, definition, legacy.Mode, captured, unlocked);
        }

        private static void WriteGrid(BinaryWriter writer, BattlefieldGridConfiguration grid)
        {
            writer.Write(grid.Origin.X); writer.Write(grid.Origin.Y); writer.Write(grid.OriginZ);
            writer.Write(grid.MapWidth); writer.Write(grid.MapHeight);
            writer.Write(grid.CellsX); writer.Write(grid.CellsY);
        }

        private static BattlefieldGridConfiguration ReadGrid(BinaryReader reader) => new BattlefieldGridConfiguration(
            new BattlefieldPoint(reader.ReadDouble(), reader.ReadDouble()), reader.ReadDouble(),
            reader.ReadDouble(), reader.ReadDouble(), reader.ReadInt32(), reader.ReadInt32());

        private sealed class State
        {
            public readonly BattlefieldGridConfiguration Battlefield;
            public readonly RegionGridDefinition Regions;
            public readonly RegionPolicyMode Mode;
            public readonly int[] Captured;
            public readonly int[] Unlocked;
            public State(BattlefieldGridConfiguration battlefield, RegionGridDefinition regions,
                RegionPolicyMode mode, int[] captured, int[] unlocked)
            { Battlefield = battlefield; Regions = regions; Mode = mode; Captured = captured; Unlocked = unlocked; }
        }
    }
}

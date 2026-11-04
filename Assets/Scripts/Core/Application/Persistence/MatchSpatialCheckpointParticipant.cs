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
        private readonly Action<RegionGridDefinition, RegionPolicyMode, int[]> _restoreRegions;
        public string Key => "world/spatial";
        public int Version => 1;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 10;

        /// <summary>Scene 조립부가 현재 Session과 지역 바인딩의 명시 복원 함수를 연결합니다.</summary>
        public MatchSpatialCheckpointParticipant(MatchSession session,
            Action<RegionGridDefinition, RegionPolicyMode, int[]> restoreRegions)
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
            State target = codec.Read(section, Version, reader => Read(reader, codec));
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
            return new State(_session.Battlefield.Grid.Configuration, regions.Definition, regions.Mode, captured);
        }

        private void Apply(State state)
        {
            _session.ReconfigureBattlefieldGrid(state.Battlefield);
            _restoreRegions(state.Regions, state.Mode, (int[])state.Captured.Clone());
        }

        private static void Write(BinaryWriter writer, State state)
        {
            WriteGrid(writer, state.Battlefield);
            WriteGrid(writer, state.Regions.Geometry);
            writer.Write((int)state.Mode);
            writer.Write(state.Regions.RegionIds.Count);
            foreach (int id in state.Regions.RegionIds) writer.Write(id);
            writer.Write(state.Captured.Length);
            foreach (int index in state.Captured) writer.Write(index);
        }

        private static State Read(BinaryReader reader, ICheckpointPayloadCodec codec)
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
                var definition = new RegionGridDefinition(geometry, ids);
                var captured = new int[codec.ReadCount(reader, count)];
                int previous = -1;
                for (int i = 0; i < captured.Length; i++)
                {
                    int index = reader.ReadInt32();
                    if (index <= previous || index >= count) throw new InvalidDataException("Invalid or duplicate captured cell.");
                    captured[i] = index;
                    previous = index;
                }
                return new State(battlefield, definition, mode, captured);
            }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid saved spatial configuration.", error); }
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
            public State(BattlefieldGridConfiguration battlefield, RegionGridDefinition regions,
                RegionPolicyMode mode, int[] captured)
            { Battlefield = battlefield; Regions = regions; Mode = mode; Captured = captured; }
        }
    }
}

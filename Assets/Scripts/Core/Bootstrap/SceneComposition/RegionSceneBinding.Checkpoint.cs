// 동적 지역의 시작 셀 override를 실제 Match 체크포인트에 보관합니다.
// 공간/actor 복원 전 설정만 적용하고, 최종 점유 계산은 조립부의 복원 완료 단계에서 수행합니다.
using System;
using System.IO;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class RegionSceneBinding : ICheckpointParticipant
    {
        string ICheckpointParticipant.Key => "world/region-start";
        int ICheckpointParticipant.Version => 2;
        CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
        int ICheckpointParticipant.RestoreOrder => 9;

        CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            RequireSettingsMatch(context.Config.MatchId);
            int[] cells = CaptureDynamicStartCells();
            Array.Sort(cells);
            return codec.Write("world/region-start", 2, CheckpointSectionKind.World, writer =>
            {
                writer.Write(cells.Length);
                foreach (int cell in cells) writer.Write(cell);
                writer.Write(_staticCapture.Count);
                foreach (var state in _staticCapture.Values)
                {
                    writer.Write(state.RegionId); writer.Write(state.HoldSeconds); writer.Write(state.TeamDamage);
                    writer.Write(state.AbsenceSeconds); writer.Write(state.SuperioritySeconds); writer.Write(state.IsSecured);
                }
            });
        }

        ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context,
            CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            MatchId expected = context.Current.Config.MatchId;
            RequireSettingsMatch(expected);
            if (section.Version < 1 || section.Version > 2) throw new InvalidDataException("Unsupported capture progress version.");
            StaticRegionCapture[] targetProgress = Array.Empty<StaticRegionCapture>();
            int[] target = codec.Read(section, section.Version, reader =>
            {
                var cells = new int[codec.ReadCount(reader, BattlefieldGridConfiguration.MaximumCellCount)];
                int previous = -1;
                for (int i = 0; i < cells.Length; i++)
                {
                    int cell = reader.ReadInt32();
                    if (cell <= previous || cell >= BattlefieldGridConfiguration.MaximumCellCount)
                        throw new InvalidDataException("Invalid or duplicate dynamic start cell.");
                    cells[i] = cell;
                    previous = cell;
                }
                if (section.Version >= 2)
                {
                    targetProgress = new StaticRegionCapture[codec.ReadCount(reader, BattlefieldGridConfiguration.MaximumCellCount)];
                    int previousId = 0;
                    for (int i = 0; i < targetProgress.Length; i++)
                    {
                        int id = reader.ReadInt32();
                        if (id <= previousId) throw new InvalidDataException("Invalid or duplicate static capture region.");
                        var state = new StaticRegionCapture(id);
                        state.Restore(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadBoolean());
                        targetProgress[i] = state; previousId = id;
                    }
                }
                return cells;
            });
            int[] backup = CaptureDynamicStartCells();
            var backupProgress = _staticCapture.Values.Select(CloneStaticProgress).ToArray();
            return new TeamHJD.Game.Application.CheckpointRestoreStep(
                () => RestoreStartCells(expected, target), () => RestoreStartCells(expected, backup),
                resolveReferences: () => RestoreStaticProgress(targetProgress),
                resolveRollbackReferences: () => RestoreStaticProgress(backupProgress));
        }

        private static StaticRegionCapture CloneStaticProgress(StaticRegionCapture source)
        {
            var copy = new StaticRegionCapture(source.RegionId);
            copy.Restore(source.HoldSeconds, source.TeamDamage, source.AbsenceSeconds, source.SuperioritySeconds, source.IsSecured);
            return copy;
        }

        private void RestoreStaticProgress(StaticRegionCapture[] states)
        {
            if (_source.Snapshot.Mode != RegionPolicyMode.Static) return;
            foreach (var saved in states)
            {
                if (!_staticCapture.TryGetValue(saved.RegionId, out var current))
                    throw new InvalidDataException("Saved capture progress has no eligible Stage region.");
                bool captured = Enumerable.Range(0, _definition.RegionIds.Count)
                    .Where(i => _definition.RegionIds[i] == saved.RegionId && _definition.CapturableCells[i])
                    .All(i => _source.Snapshot.CapturedCells[i]);
                // 매우 작은 시험 Grid에서 CU 시작 셀만으로 구역 전체가 채워져도 전투 확보 완료는 아닙니다.
                var seed = InitialControlUnitCells(_definition);
                bool seedOnly = Enumerable.Range(0, _definition.RegionIds.Count)
                    .Where(i => _definition.RegionIds[i] == saved.RegionId && _definition.CapturableCells[i])
                    .All(i => seed.Contains(i));
                if (captured != saved.IsSecured && !(captured && !saved.IsSecured && seedOnly))
                    throw new InvalidDataException("Saved capture progress conflicts with the spatial capture state.");
                current.Restore(saved.HoldSeconds, saved.TeamDamage, saved.AbsenceSeconds, saved.SuperioritySeconds, saved.IsSecured);
            }
        }

        private void RequireSettingsMatch(MatchId expected)
        {
            if (!HasWriteAuthority || _session.Config.MatchId != expected)
                throw new InvalidOperationException("Dynamic start settings require their current authoritative Match.");
        }

        private void RestoreStartCells(MatchId expected, int[] cells)
        {
            RequireSettingsMatch(expected);
            _dynamicInitialCells = cells.Length == 0 ? null : (int[])cells.Clone();
            _lastInputs = null;
            _lastControlUnitPosition = null;
            _dirty = true;
        }
    }
}

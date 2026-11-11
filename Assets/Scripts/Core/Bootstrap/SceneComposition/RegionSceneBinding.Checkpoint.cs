// 동적 지역의 시작 셀 override를 실제 Match 체크포인트에 보관합니다.
// 공간/actor 복원 전 설정만 적용하고, 최종 점유 계산은 조립부의 복원 완료 단계에서 수행합니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class RegionSceneBinding : ICheckpointParticipant
    {
        string ICheckpointParticipant.Key => "world/region-start";
        int ICheckpointParticipant.Version => 1;
        CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
        int ICheckpointParticipant.RestoreOrder => 9;

        CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            RequireSettingsMatch(context.Config.MatchId);
            int[] cells = CaptureDynamicStartCells();
            Array.Sort(cells);
            return codec.Write("world/region-start", 1, CheckpointSectionKind.World, writer =>
            {
                writer.Write(cells.Length);
                foreach (int cell in cells) writer.Write(cell);
            });
        }

        ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context,
            CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            MatchId expected = context.Current.Config.MatchId;
            RequireSettingsMatch(expected);
            int[] target = codec.Read(section, 1, reader =>
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
                return cells;
            });
            int[] backup = CaptureDynamicStartCells();
            return new TeamHJD.Game.Application.CheckpointRestoreStep(
                () => RestoreStartCells(expected, target), () => RestoreStartCells(expected, backup));
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

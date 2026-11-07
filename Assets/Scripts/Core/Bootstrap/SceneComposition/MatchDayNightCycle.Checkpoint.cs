// 낮밤 구간 길이와 시작 기준을 저장하고 복원된 Core 시각에서 단계를 다시 계산합니다.
// 복원은 PhaseAdvanced를 발생시키지 않으므로 Weather 큐를 새로 소비하지 않습니다.
using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class MatchDayNightCycle : ICheckpointParticipant
    {
        public string Key => "world/time-of-day";
        public int Version => 2;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 390;

        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            RequireCheckpoint(context);
            return codec.Write(Key, Version, Kind, writer =>
            {
                writer.Write(_cycle.DaySeconds);
                writer.Write(_cycle.NightSeconds);
                writer.Write(_cycle.EpochSeconds);
                writer.Write((byte)_cycle.InitialPhase);
            });
        }

        public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            RequireCheckpoint(context.Current);
            if (section.Version != 1 && section.Version != Version)
                throw new NotSupportedException("Unsupported day/night checkpoint version: " + section.Version);
            // 기존 v1은 Day 시작 기준입니다. v2는 Night 중 Reset한 시작 구간도 보존합니다.
            var saved = codec.Read(section, section.Version, reader => new DayNightCycle(
                reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(),
                section.Version == 1 ? DayNightPhase.Day : (DayNightPhase)reader.ReadByte()));
            saved.GetPhase(context.Target.GameSeconds);
            var backup = _cycle;
            return new CheckpointRestoreStep(() => RestoreCycle(saved), () => RestoreCycle(backup));
        }

        private void RequireCheckpoint(CheckpointCaptureContext context)
        {
            if (!IsAuthority || context.Config.MatchId != _match || _gameplay.IsCancellationRequested)
                throw new InvalidOperationException("Capture day/night from its current authority Match.");
        }

        private void RestoreCycle(DayNightCycle cycle)
        {
            _cycle = cycle;
            daySeconds = cycle.DaySeconds;
            nightSeconds = cycle.NightSeconds;
            ApplyCurrentTime(false);
            ScheduleNext();
            Changed?.Invoke();
        }
    }
}

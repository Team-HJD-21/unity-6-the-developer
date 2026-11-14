// 공용 시각과 별개로 기존 Field의 보류 시각을 보관합니다.
// actor의 냉각·공격·투사체 상태보다 먼저 복원해 남은 deadline이 흐르지 않게 합니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class FieldTimeCheckpointParticipant : ICheckpointParticipant
    {
        private readonly FieldSimulationClock _clock;
        public string Key => "world/field-time";
        public int Version => 1;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 1;
        public FieldTimeCheckpointParticipant(FieldSimulationClock clock)
        { _clock = clock ?? throw new ArgumentNullException(nameof(clock)); }
        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            RequireMatch(context.Config.MatchId);
            return codec.Write(Key, Version, Kind, w => { w.Write(_clock.ElapsedSeconds); w.Write(_clock.IsHeld); });
        }
        public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            RequireMatch(context.Current.Config.MatchId);
            if (section.Key != Key || section.Kind != Kind) throw new InvalidDataException("Unexpected Field time section.");
            var target = codec.Read(section, Version, r => (seconds: r.ReadDouble(), held: r.ReadBoolean()));
            if (!double.IsFinite(target.seconds) || target.seconds < 0 || target.seconds > context.Target.GameSeconds + .000001)
                throw new InvalidDataException("Invalid Field elapsed time.");
            double backup = _clock.ElapsedSeconds; bool wasHeld = _clock.IsHeld;
            return new ClockRestoreStep(() => _clock.Restore(target.seconds, target.held), () => _clock.Restore(backup, wasHeld));
        }
        private sealed class ClockRestoreStep : ICheckpointClockRestoreStep
        {
            private readonly Action _apply, _rollback;
            public ClockRestoreStep(Action apply, Action rollback) { _apply = apply; _rollback = rollback; }
            public void Apply() => _apply();
            public void Rollback() => _rollback();
            public void Dispose() { }
        }
        private void RequireMatch(MatchId match)
        { if (_clock.Snapshot.MatchId != match) throw new InvalidOperationException("Field time belongs to a different Match."); }
    }
}

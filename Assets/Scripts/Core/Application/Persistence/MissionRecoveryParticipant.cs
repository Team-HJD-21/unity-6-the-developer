// 구조/안전장치의 비환급 원본을 Host 성장 파일에 연결합니다. 월드 writer로 등록하지 않습니다.
// 실제 대기 시간은 합쳐 저장하고 실패·복귀·정상 종료에는 즉시/Flush로 확정합니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class MissionRecoveryParticipant : IGrowthCheckpointParticipant
    {
        private readonly MissionRecoveryLedger _ledger;
        private event Action<GrowthSaveUrgency> _changed;
        public MissionRecoveryParticipant(MissionRecoveryLedger ledger) => _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        public string Key => "growth/mission-recovery";
        public string PersistenceKey => "mission-recovery";
        public int Version => 1;
        public int RestoreOrder => -10;
        public CheckpointSectionKind Kind => CheckpointSectionKind.Progression;
        public event Action<GrowthSaveUrgency> GrowthChanged
        {
            add
            {
                if (_changed == null) { _ledger.RescueTimeChanged += TimeChanged; _ledger.CriticalChanged += CriticalChanged; }
                _changed += value;
            }
            remove
            {
                _changed -= value;
                if (_changed == null) { _ledger.RescueTimeChanged -= TimeChanged; _ledger.CriticalChanged -= CriticalChanged; }
            }
        }
        private void TimeChanged() => _changed?.Invoke(GrowthSaveUrgency.Coalesced);
        private void CriticalChanged() => _changed?.Invoke(GrowthSaveUrgency.Immediate);
        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
            => codec.Write(Key, 1, Kind, w =>
            {
                codec.WriteText(w, context.Config.StageId.Value); codec.WriteText(w, _ledger.RunId.ToString("N"));
                w.Write(_ledger.RescueSeconds); w.Write(_ledger.SafetyUses); codec.WriteText(w, _ledger.PendingFailure.ToString("N"));
                w.Write(_ledger.IsEnded); codec.WriteText(w, _ledger.LastCheckpoint.Value ?? "");
                codec.WriteText(w, _ledger.LastCheckpointId.ToString("N"));
            });
        public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            var saved = codec.Read(section, 1, r =>
            {
                if (codec.ReadText(r) != context.Current.Config.StageId.Value) throw new InvalidDataException("The recovery ledger belongs to another Stage.");
                var run = Guid.ParseExact(codec.ReadText(r), "N"); double time = r.ReadDouble(); int uses = r.ReadInt32();
                var pending = Guid.ParseExact(codec.ReadText(r), "N"); bool ended = r.ReadBoolean();
                string slot = codec.ReadText(r); var checkpoint = Guid.ParseExact(codec.ReadText(r), "N");
                var result = new MissionRecoveryLedger();
                result.Restore(run, time, uses, pending, ended, slot.Length == 0 ? default : new CheckpointSlot(slot), checkpoint);
                return result;
            });
            var backup = new MissionRecoveryLedger(); Copy(_ledger, backup);
            return new CheckpointRestoreStep(() => Copy(saved, _ledger), () => Copy(backup, _ledger));
        }
        private static void Copy(MissionRecoveryLedger source, MissionRecoveryLedger target)
            => target.Restore(source.RunId, source.RescueSeconds, source.SafetyUses, source.PendingFailure,
                source.IsEnded, source.LastCheckpoint, source.LastCheckpointId);
    }
}

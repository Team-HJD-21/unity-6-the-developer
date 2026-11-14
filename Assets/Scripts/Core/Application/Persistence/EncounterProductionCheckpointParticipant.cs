// Match의 정규 생산 남은 대기를 월드 체크포인트에 보관합니다.
// 공용 시계 복원 뒤 새 Scheduler 세대에 예약하며 기술 실패의 rollback에도 같은 경계를 사용합니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class EncounterProductionCheckpointParticipant : ICheckpointParticipant
    {
        private readonly MatchId _match;
        private readonly MatchEncounterProduction _production;
        public string Key => "world/encounter-production";
        public int Version => 1;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 310;
        public EncounterProductionCheckpointParticipant(MatchId match, MatchEncounterProduction production)
        {
            if (match.IsEmpty) throw new ArgumentException("A Match is required.", nameof(match));
            _match = match; _production = production ?? throw new ArgumentNullException(nameof(production));
        }
        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            RequireMatch(context.Config.MatchId);
            var state = _production.Capture();
            return codec.Write(Key, Version, Kind, w => { w.Write(state.RemainingSeconds); w.Write(state.AttemptedRequests); });
        }
        public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            RequireMatch(context.Current.Config.MatchId);
            if (section.Key != Key || section.Kind != Kind) throw new InvalidDataException("Unexpected production section.");
            var state = codec.Read(section, Version, r => new MatchEncounterProduction.State(r.ReadDouble(), r.ReadInt64()));
            var backup = _production.Capture();
            return new CheckpointRestoreStep(() => _production.Restore(state), () => _production.Restore(backup));
        }
        private void RequireMatch(MatchId match)
        { if (match != _match) throw new InvalidOperationException("Production belongs to a different Match."); }
    }
}

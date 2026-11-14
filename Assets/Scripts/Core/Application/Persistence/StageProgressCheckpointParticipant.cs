// 월드 체크포인트에 해당 시점의 Stage 사건 생성·사망 확인을 보존합니다.
// 개인 성장/안전장치의 비환급 기록과 달리 과거 월드로 함께 되돌아갑니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class StageProgressCheckpointParticipant : ICheckpointParticipant
    {
        private readonly StageProgression _progress;
        public StageProgressCheckpointParticipant(StageProgression progress) => _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        public string Key => "world/stage-progress";
        public int Version => 1;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 900;
        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            var saved = _progress.Snapshot;
            return codec.Write(Key, Version, Kind, w =>
            {
                codec.WriteText(w, context.Config.StageId.Value); codec.WriteText(w, saved.RunId.ToString("N"));
                w.Write(saved.CaptureRatio); w.Write(saved.HighestCaptureRatio);
                codec.WriteText(w, saved.FirstFieldBoss.Value ?? ""); codec.WriteText(w, saved.SecondFieldBoss.Value ?? "");
                codec.WriteText(w, saved.FinalBoss.Value ?? "");
                w.Write(saved.FirstFieldBossDefeated); w.Write(saved.SecondFieldBossDefeated); w.Write(saved.FinalBossDefeated);
            });
        }
        public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            var saved = codec.Read(section, Version, r =>
            {
                if (codec.ReadText(r) != context.Current.Config.StageId.Value) throw new InvalidDataException("Stage progress belongs to another Stage.");
                var run = Guid.ParseExact(codec.ReadText(r), "N"); double current = r.ReadDouble(), highest = r.ReadDouble();
                string first = codec.ReadText(r), second = codec.ReadText(r), final = codec.ReadText(r);
                return new StageProgressSnapshot(context.Current.Config.MatchId, run, current, highest,
                    first.Length == 0 ? default : new EntityId(first), second.Length == 0 ? default : new EntityId(second),
                    final.Length == 0 ? default : new EntityId(final), r.ReadBoolean(), r.ReadBoolean(), r.ReadBoolean(), null);
            });
            var before = _progress.Snapshot;
            // Prepare 단계에서 검증하되 실제 진행은 바꾸지 않습니다.
            _progress.ValidateRestore(saved);
            return new CheckpointRestoreStep(() => _progress.Restore(saved), () => _progress.Restore(before));
        }
    }
}

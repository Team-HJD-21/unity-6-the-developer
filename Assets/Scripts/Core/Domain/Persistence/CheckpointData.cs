// 재실행 후 새 Match에 복원할 실행 입력·게임 초·Feature 저장 자료입니다.
// SourceMatchId는 진단용이며 과거 연결이나 Match 소유권을 재사용하는 값이 아닙니다.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace TeamHJD.Game.Domain
{
    public sealed class CheckpointData
    {
        public const int MaximumSections = 128, MaximumPayloadBytes = 16 * 1024 * 1024;
        public Guid CheckpointId { get; }
        public DateTimeOffset CapturedAtUtc { get; }
        public MatchId SourceMatchId { get; }
        public StageId StageId { get; }
        public DefinitionId DifficultyProfileId { get; }
        public MatchMode Mode { get; }
        public int RandomSeed { get; }
        public string ContentVersion { get; }
        public double GameSeconds { get; }
        public int ParticipantCount { get; }
        public IReadOnlyList<CheckpointSection> Sections { get; }
        public CheckpointData(Guid checkpointId, DateTimeOffset capturedAtUtc, MatchId sourceMatchId, StageId stage,
            DefinitionId difficulty, MatchMode mode, int randomSeed, string contentVersion, double gameSeconds,
            int participantCount, IEnumerable<CheckpointSection> sections)
        {
            if (checkpointId == Guid.Empty || sourceMatchId.IsEmpty || stage.IsEmpty || difficulty.IsEmpty)
                throw new ArgumentException("Checkpoint and content identities are required.");
            if (sourceMatchId.Value.Length > 128 || stage.Value.Length > 128 || difficulty.Value.Length > 128)
                throw new ArgumentException("Checkpoint identities exceed their storage limits.");
            if (!Enum.IsDefined(typeof(MatchMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (string.IsNullOrWhiteSpace(contentVersion) || contentVersion.Length > 256) throw new ArgumentException("A bounded content version is required.", nameof(contentVersion));
            if (double.IsNaN(gameSeconds) || double.IsInfinity(gameSeconds) || gameSeconds < 0) throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            if (participantCount < 1 || participantCount > 64) throw new ArgumentOutOfRangeException(nameof(participantCount));
            if (sections == null) throw new ArgumentNullException(nameof(sections));
            var copied = new List<CheckpointSection>(); var keys = new HashSet<string>(StringComparer.Ordinal); long bytes = 0;
            foreach (var section in sections)
            {
                if (section == null || !keys.Add(section.Key)) throw new ArgumentException("Feature sections require unique keys.", nameof(sections));
                bytes += section.PayloadLength;
                if (copied.Count >= MaximumSections || bytes > MaximumPayloadBytes) throw new ArgumentException("Checkpoint data exceeds its storage limits.", nameof(sections));
                copied.Add(section);
            }
            if (copied.Count == 0) throw new ArgumentException("At least one Feature section is required.", nameof(sections));
            CheckpointId = checkpointId; CapturedAtUtc = capturedAtUtc.ToUniversalTime(); SourceMatchId = sourceMatchId;
            StageId = stage; DifficultyProfileId = difficulty; Mode = mode; RandomSeed = randomSeed;
            ContentVersion = contentVersion; GameSeconds = gameSeconds; ParticipantCount = participantCount;
            Sections = new ReadOnlyCollection<CheckpointSection>(copied);
        }
    }
}

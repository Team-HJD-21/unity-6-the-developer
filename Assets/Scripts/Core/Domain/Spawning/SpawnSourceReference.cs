// 논리 source의 Match와 등록 세대를 함께 식별해 삭제 후 같은 ID의 재등록을 구분합니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class SpawnSourceReference
    {
        public MatchId MatchId { get; }
        public DefinitionId SourceId { get; }
        public Guid Generation { get; }
        public SpawnSourceReference(MatchId matchId, DefinitionId sourceId, Guid generation)
        {
            if (matchId.IsEmpty || sourceId.IsEmpty || sourceId.Value.Length > 128 || generation == Guid.Empty)
                throw new ArgumentException("A Match, bounded source ID and generation are required.");
            MatchId = matchId; SourceId = sourceId; Generation = generation;
        }
        public string GetPayloadKey() => $"{MatchId.Value.Length}:{MatchId.Value}:{SourceId.Value.Length}:{SourceId.Value}:{Generation:N}";
    }
}

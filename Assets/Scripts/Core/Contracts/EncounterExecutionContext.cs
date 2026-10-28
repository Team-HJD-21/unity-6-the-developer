// 요청과 실제 생성/제거를 연관시키는 값입니다. Spawn 정책이나 Unity 객체는 담지 않습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class EncounterExecutionContext
    {
        public MatchId MatchId { get; }
        public long RequestId { get; }
        public long Revision { get; }
        public EncounterRequestSource Source { get; }
        public string SquadOrder { get; }
        public MatchEnvironmentSnapshot Environment { get; }
        public EncounterExecutionContext(MatchId matchId, long requestId, long revision, EncounterRequestSource source, string squadOrder = "",
            MatchEnvironmentSnapshot environment = null)
        {
            if (environment != null && environment.MatchId != matchId)
                throw new System.ArgumentException("Environment must belong to the execution Match.", nameof(environment));
            MatchId = matchId; RequestId = requestId; Revision = revision; Source = source; SquadOrder = squadOrder; Environment = environment;
        }
    }
}

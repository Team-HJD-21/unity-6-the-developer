// 요청과 실제 생성/제거를 연관시키는 값입니다. Spawn 정책이나 Unity 객체는 담지 않습니다.
using TeamHJD.Game.Domain;
using System;
using System.Threading;
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
        public Guid RequestGuid { get; }
        public Guid CauseEventId { get; }
        public EncounterSpawnPlacement Placement { get; }
        public EncounterSpawnOrigin Origin { get; }
        public int? MatchSeed { get; }
        public CancellationToken SpawnCancellation { get; }
        public EncounterExecutionContext(MatchId matchId, long requestId, long revision, EncounterRequestSource source, string squadOrder = "",
            MatchEnvironmentSnapshot environment = null, Guid requestGuid = default, Guid causeEventId = default,
            EncounterSpawnPlacement placement = null, int? matchSeed = null, CancellationToken spawnCancellation = default,
            EncounterSpawnOrigin origin = null)
        {
            if (environment != null && environment.MatchId != matchId)
                throw new System.ArgumentException("Environment must belong to the execution Match.", nameof(environment));
            MatchId = matchId; RequestId = requestId; Revision = revision; Source = source; SquadOrder = squadOrder; Environment = environment;
            RequestGuid = requestGuid; CauseEventId = causeEventId; Placement = placement;
            Origin = origin ?? placement?.Origin ?? EncounterSpawnOrigin.Automatic;
            MatchSeed = matchSeed; SpawnCancellation = spawnCancellation;
        }
    }
}

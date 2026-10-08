// Feature가 확정한 사실을 Editor로 전달하는 불변 기록입니다.
// 시간은 Match 시작 이후 게임/실제 초이며 수량은 계획과 실행을 구분합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class GameDiagnosticEvent
    {
        public long Sequence { get; }
        public MatchId MatchId { get; }
        public long Revision { get; }
        public long RequestId { get; }
        public EncounterRequestSource Source { get; }
        public GameDiagnosticKind Kind { get; }
        public double GameSeconds { get; }
        public double RealSeconds { get; }
        public string EnemyType { get; }
        public string SpawnPointId { get; }
        public string EntityId { get; }
        public int PlannedCount { get; }
        public int ActualCount { get; }
        public string Message { get; }
        public string SquadOrder { get; }
        public SpawnDecisionSnapshot SpawnDecision { get; }
        public bool HasDeathEvents { get; }
        public bool IsSpawnDecisionOmitted { get; }
        public GameDiagnosticEvent(long sequence, MatchId matchId, long revision, long requestId,
            EncounterRequestSource source, GameDiagnosticKind kind, double gameSeconds, double realSeconds,
            string enemyType = "", string spawnPointId = "", string entityId = "",
            int plannedCount = 0, int actualCount = 0, string message = "", string squadOrder = "", SpawnDecisionSnapshot spawnDecision = null,
            bool hasDeathEvents = false, bool isSpawnDecisionOmitted = false)
        {
            Sequence = sequence; MatchId = matchId; Revision = revision; RequestId = requestId;
            Source = source; Kind = kind; GameSeconds = gameSeconds; RealSeconds = realSeconds;
            EnemyType = enemyType; SpawnPointId = spawnPointId; EntityId = entityId;
            PlannedCount = plannedCount; ActualCount = actualCount; Message = message;
            SquadOrder = squadOrder;
            SpawnDecision = spawnDecision;
            HasDeathEvents = hasDeathEvents;
            IsSpawnDecisionOmitted = isSpawnDecisionOmitted;
        }
        public GameDiagnosticEvent WithoutSpawnDecision() => new GameDiagnosticEvent(Sequence,MatchId,Revision,RequestId,Source,
            Kind,GameSeconds,RealSeconds,EnemyType,SpawnPointId,EntityId,PlannedCount,ActualCount,Message,SquadOrder,null,HasDeathEvents,true);
    }
}

// Executor의 실제 생성 성공/제거 사실입니다. 미연결 독립 시험은 Context가 null입니다.
namespace TeamHJD.Game.Contracts
{
    public sealed class EnemyLifecycleObservation
    {
        public EncounterExecutionContext Context { get; }
        public GameDiagnosticKind Kind { get; }
        public string EnemyType { get; }
        public string SpawnPointId { get; }
        public string EntityId { get; }
        public string Reason { get; }
        public EnemyLifecycleObservation(EncounterExecutionContext context, GameDiagnosticKind kind,
            string enemyType, string spawnPointId, string entityId, string reason)
        { Context = context; Kind = kind; EnemyType = enemyType; SpawnPointId = spawnPointId; EntityId = entityId; Reason = reason; }
    }
}

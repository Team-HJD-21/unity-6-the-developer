// 적 개체가 처치되었음을 기록합니다.


namespace TeamHJD.Game.Domain
{
    public sealed class EnemyDefeated : MatchEvent
    {
        public EntityId EnemyId { get; }

        public EnemyDefeated(MatchId matchId, long sequence, EntityId enemyId)
            : base(matchId, sequence) => EnemyId = enemyId;
    }
}

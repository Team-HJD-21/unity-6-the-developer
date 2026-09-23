// Records that an enemy actor has been defeated.


namespace TeamHJD.Game.Domain
{
    public sealed class EnemyDefeated : MatchEvent
    {
        public EntityId EnemyId { get; }

        public EnemyDefeated(MatchId matchId, long sequence, EntityId enemyId)
            : base(matchId, sequence) => EnemyId = enemyId;
    }
}

// 적 개체에 피해가 적용되었음을 기록합니다.


namespace TeamHJD.Game.Domain
{
    public sealed class EnemyDamaged : MatchEvent
    {
        public EntityId EnemyId { get; }
        public int DamageAmount { get; }
        public int RemainingHealth { get; }

        public EnemyDamaged(MatchId matchId, long sequence, EntityId enemyId, int damageAmount, int remainingHealth)
            : base(matchId, sequence)
        {
            EnemyId = enemyId;
            DamageAmount = damageAmount;
            RemainingHealth = remainingHealth;
        }
    }
}

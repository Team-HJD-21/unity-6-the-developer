// Records damage already applied to an enemy actor.


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

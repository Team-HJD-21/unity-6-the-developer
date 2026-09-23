// Records the resulting player health after a state transition.


namespace TeamHJD.Game.Domain
{
    public sealed class PlayerHealthChanged : MatchEvent
    {
        public PlayerId PlayerId { get; }
        public int PreviousHealth { get; }
        public int CurrentHealth { get; }

        public PlayerHealthChanged(MatchId matchId, long sequence, PlayerId playerId, int previousHealth, int currentHealth)
            : base(matchId, sequence)
        {
            PlayerId = playerId;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
        }
    }
}

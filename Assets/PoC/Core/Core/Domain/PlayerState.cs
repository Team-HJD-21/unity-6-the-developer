// Immutable runtime values owned by one player slot in a match.


namespace TeamHJD.Game.Domain
{
    public sealed class PlayerState
    {
        public PlayerId PlayerId { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public bool IsAlive { get; }

        public PlayerState(PlayerId playerId, int health, int maxHealth, bool isAlive)
        {
            PlayerId = playerId;
            Health = health;
            MaxHealth = maxHealth;
            IsAlive = isAlive;
        }
    }
}

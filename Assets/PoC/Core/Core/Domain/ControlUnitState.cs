// Immutable runtime values for the defended control unit.


namespace TeamHJD.Game.Domain
{
    public sealed class ControlUnitState
    {
        public int Health { get; }
        public int MaxHealth { get; }
        public int Power { get; }
        public int MaxPower { get; }

        public ControlUnitState(int health, int maxHealth, int power, int maxPower)
        {
            Health = health;
            MaxHealth = maxHealth;
            Power = power;
            MaxPower = maxPower;
        }
    }
}

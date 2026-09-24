// 방어 대상 제어 장치의 불변 런타임 값입니다.


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

namespace TeamHJD.Game.FieldTurrets.Contracts
{
    /// <summary>몬스터가 구체 타입을 몰라도 피해를 적용할 수 있는 전투 대상 계약이다.</summary>
    public interface IDamageableTarget
    {
        int CurrentHealth { get; }
        int MaxHealth { get; }
        bool IsDestroyed { get; }
        bool ApplyDamage(int damage);
    }
}

namespace TeamHJD.Game.Enemy.Contracts
{
    /// <summary>적의 공격 허용 여부 조회와 피해 적용을 제공한다.</summary>
    public interface IAttackableEnemy
    {
        /// <summary>해당 공격자가 이 적을 공격할 수 있는지 반환한다.</summary>
        bool CanBeAttackedBy(AttackSourceType source);

        /// <summary>공격 허용 여부를 검사하고 피해를 적용한다.</summary>
        bool TryTakeDamage(int damage, AttackSourceType source);
    }
}

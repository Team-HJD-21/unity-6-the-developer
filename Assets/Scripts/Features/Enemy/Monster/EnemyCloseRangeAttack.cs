using UnityEngine;

/// <summary>
/// 공격 범위에 들어온 타깃에게 즉시 피해를 준다.
/// </summary>
public sealed class EnemyCloseRangeAttack : EnemyAttack
{
    public override bool Execute(TargetableComponent target, int damage)
    {
        if (target == null || damage <= 0)
            return false;

        if (target.TargetType == TargetType.Turret)
            return target.TryApplyDamage(damage);

        if (target.TargetType != TargetType.Player)
            return false;

        PlayerInfo player = target.GetComponentInParent<PlayerInfo>();
        if (player == null)
            return false;

        player.TakeDamage(damage);
        return true;
    }
}

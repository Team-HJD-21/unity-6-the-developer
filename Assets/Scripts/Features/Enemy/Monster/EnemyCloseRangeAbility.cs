using UnityEngine;

/// <summary>
/// 사거리 안의 플레이어나 터렛에 즉시 피해를 주는 근접 능력이다.
/// </summary>
public class EnemyCloseRangeAbility : EnemyAbility
{
    [SerializeField] private EnemyTargeting enemyTargeting;
    [Header("공격 설정")]
    [SerializeField] private int attackDamage = 5;

    protected override void Awake()
    {
        base.Awake();

        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
    }

    public override bool CheckCondition()
    {
        return attackDamage > 0 &&
               TryGetTarget(out _) &&
               IsInRange(enemyTargeting.TargetColliders, activationRange);
    }

    protected override bool Activate()
    {
        if (!TryGetTarget(out TargetableComponent target))
            return false;

        return ApplyDamage(target);
    }

    /// <summary>
    /// 타깃 유형에 맞는 체력 시스템에 피해를 전달한다.
    /// </summary>
    protected bool ApplyDamage(TargetableComponent target)
    {
        if (target == null || !target.CanBeTargeted)
            return false;

        if (target.TargetType == TargetType.Turret)
            return target.TryApplyDamage(attackDamage);

        if (target.TargetType != TargetType.Player)
            return false;

        PlayerInfo player = target.GetComponentInParent<PlayerInfo>();
        if (player == null)
            return false;

        player.TakeDamage(attackDamage);
        return true;
    }

    protected bool TryGetTarget(out TargetableComponent target)
    {
        target = null;
        if (enemyTargeting == null || enemyTargeting.Target == null)
            return false;

        target = enemyTargeting.Target.GetComponent<TargetableComponent>();
        return target != null && target.CanBeTargeted;
    }
}

// 근접 발동 조건을 확인하고 Player·터렛의 공통 피해 수신자에 서버 피해를 전달합니다.
using UnityEngine;

/// <summary>
/// 사거리 안의 플레이어나 터렛에 즉시 피해를 주는 근접 능력이다.
/// </summary>
public sealed class EnemyCloseRangeAbility : EnemyAbility
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

        // Player와 Turret 모두 실제 HP 원본의 동일한 피해 계약으로 전달합니다.
        return target.TryApplyDamage(attackDamage);
    }

    private bool TryGetTarget(out TargetableComponent target)
    {
        target = null;
        if (enemyTargeting == null || enemyTargeting.Target == null)
            return false;

        target = enemyTargeting.Target.GetComponent<TargetableComponent>();
        return target != null && target.CanBeTargeted;
    }
}

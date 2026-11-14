// 근접 발동 조건을 확인하고 Player·터렛의 공통 피해 수신자에 서버 피해를 전달합니다.
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

    /// <summary>
    /// 유효한 타깃의 콜라이더가 근접 범위 안에 있는지 확인한다.
    /// </summary>
    public override bool CheckCondition()
    {
        return enemyTargeting != null &&
               CheckCondition(IsInRange(enemyTargeting.TargetColliders));
    }

    /// <summary>이번 행동에서 계산한 거리 결과로 근접 발동 조건을 확인한다.</summary>
    public override bool CheckCondition(bool isInTargetRange)
    {
        return isInTargetRange &&
               attackDamage > 0 &&
               TryGetTarget(out _);
    }

    /// <summary>
    /// 현재 타깃에 공통 피해 수신 계약으로 근접 피해를 전달한다.
    /// </summary>
    protected override bool Activate()
    {
        if (!TryGetTarget(out TargetableComponent target))
            return false;

        // Player와 Turret 모두 실제 HP 원본의 동일한 피해 계약으로 전달합니다.
        return ApplyDamage(target);
    }

    /// <summary>근접 공격과 파생 능력이 같은 피해 수신 계약을 사용한다.</summary>
    protected bool ApplyDamage(TargetableComponent target)
    {
        return target != null && target.CanBeTargeted && target.TryApplyDamage(attackDamage);
    }

    /// <summary>
    /// 현재 배정된 타깃에서 공격 가능한 피해 수신 컴포넌트를 찾는다.
    /// </summary>
    protected bool TryGetTarget(out TargetableComponent target)
    {
        target = enemyTargeting != null
            ? enemyTargeting.TargetComponent
            : null;

        return target != null;
    }
}

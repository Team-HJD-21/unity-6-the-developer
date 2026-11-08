using System.Collections;
using UnityEngine;

/// <summary>
/// 공격 준비 시간이 지난 뒤 사거리 안의 타깃에 피해를 준다.
/// </summary>
public sealed class EnemyAssassinAbility : EnemyCloseRangeAbility
{
    [Header("공격 준비")]
    [SerializeField, Min(0f)] private float attackWindupDuration = 0.3f;

    private bool _isExecuting;

    public override bool IsExecuting => _isExecuting;

    protected override bool Activate()
    {
        if (!TryGetTarget(out TargetableComponent target))
            return false;

        _isExecuting = true;
        StartCoroutine(AttackAfterWindup(target));
        return true;
    }

    private IEnumerator AttackAfterWindup(TargetableComponent target)
    {
        // TODO: 공용 시간 통합 시 준비 대기를 Match 시계와 필드 정지에 연결한다.
        yield return new WaitForSeconds(attackWindupDuration);

        if (target != null && target.CanBeTargeted)
        {
            Collider2D[] targetColliders =
                target.GetComponentsInChildren<Collider2D>(true);

            if (IsInRange(targetColliders, activationRange))
                ApplyDamage(target);
        }

        _isExecuting = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _isExecuting = false;
    }
}

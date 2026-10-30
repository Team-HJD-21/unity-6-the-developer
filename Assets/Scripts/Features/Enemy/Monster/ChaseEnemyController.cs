using UnityEngine;

/// <summary>
/// 타깃을 추적하고 공격 범위 안에서는 이동을 멈춰 공격한다.
/// 근접과 원거리의 실제 공격 방식은 전투 컴포넌트에 맡긴다.
/// </summary>
public class ChaseEnemyController : MovingEnemyController
{
    [Header("참조")]
    [SerializeField] private EnemyCombat enemyCombat;

    protected override void Awake()
    {
        base.Awake();
        if (enemyCombat == null)
            enemyCombat = GetComponent<EnemyCombat>();
    }

    /// <summary>
    /// 타깃과 경로, 공격 범위에 따라 대기하거나 이동·공격한다.
    /// </summary>
    public override void ExecuteBehavior()
    {
        // 타겟이 없으면 이동을 멈추고 대기한다.
        if (enemyTargeting.Target == null)
        {
            StopMoving();
            return;
        }
        
        // 공격 범위 안에서는 이동을 멈추고 공격을 시도한다.
        if (enemyCombat.IsInAttackRange())
        {
            StopMoving();

            Vector2 attackDirection =
                (Vector2)enemyTargeting.Target.position - (Vector2)transform.position;

            if (attackDirection.sqrMagnitude > 0.0001f)
                enemyAnimation.SetFacingDirection(attackDirection);

            TryAttack();
            return;
        }
        
        if (!enemyMovement.TryGetNextPathDirection(out Vector2 moveDirection))
        {
            StopMoving();
            return;
        }

        enemyAnimation.SetFacingDirection(moveDirection);
        MoveTowardsTarget(moveDirection);
    }
    
    /// <summary>
    /// 전투 처리가 성공하면 공격 애니메이션을 재생한다.
    /// </summary>
    private void TryAttack()
    {
        if (!enemyCombat.TryAttack())
            return;

        enemyAnimation.PlayAttackAnimation();
    }
}

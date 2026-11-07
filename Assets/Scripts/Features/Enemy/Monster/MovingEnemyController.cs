using UnityEngine;

/// <summary>
/// 능력의 발동 조건에 따라 타깃으로 이동하거나 정지하고 능력을 사용한다.
/// 실제 이동과 능력 효과는 각 컴포넌트에 맡긴다.
/// </summary>
public class MovingEnemyController : EnemyController
{
    [Header("이동")]
    [SerializeField] protected EnemyMovement enemyMovement;

    protected override void Awake()
    {
        base.Awake();
        if (enemyMovement == null)
            enemyMovement = GetComponent<EnemyMovement>();
    }

    /// <summary>
    /// 발동 조건을 충족하면 정지하고 능력을 시도하며, 충족하지 않으면 타깃으로 이동한다.
    /// </summary>
    public override void ExecuteBehavior()
    {
        if (enemyAbility == null || !enemyAbility.isActiveAndEnabled)
        {
            StopMoving();
            return;
        }

        if (enemyAbility.IsExecuting)
        {
            StopMoving();
            return;
        }

        if (enemyAbility.CheckCondition())
        {
            StopMoving();

            Transform facingTarget = enemyAbility.FacingTarget;
            if (facingTarget == null)
                facingTarget = enemyTargeting.Target;

            if (facingTarget != null)
            {
                Vector2 abilityDirection =
                    (Vector2)facingTarget.position - (Vector2)transform.position;

                if (abilityDirection.sqrMagnitude > 0.0001f)
                    enemyAnimation.SetFacingDirection(abilityDirection);
            }

            TryUseAbility();
            return;
        }

        MoveToTarget();
    }

    /// <summary>배정된 타깃으로 이동하고 범위에 도착하면 멈춘다.</summary>
    protected void MoveToTarget()
    {
        if (enemyTargeting.Target == null ||
            enemyAbility.IsInRange(enemyTargeting.TargetColliders) ||
            !enemyMovement.TryGetNextPathDirection(out Vector2 moveDirection))
        {
            StopMoving();
            return;
        }

        enemyAnimation.SetFacingDirection(moveDirection);
        MoveTowardsTarget(moveDirection);
    }

    /// <summary>
    /// 지정된 방향으로 이동하고 이동 애니메이션 속도를 갱신한다.
    /// </summary>
    /// <param name="moveDirection">정규화된 이동 방향.</param>
    protected void MoveTowardsTarget(Vector2 moveDirection)
    {
        enemyAnimation.PlayMovementAnimation(enemyMovement.MoveSpeed);
        enemyMovement.Move(moveDirection);
    }

    /// <summary>
    /// 몬스터 이동을 멈추고 대기 애니메이션으로 전환한다.
    /// </summary>
    protected void StopMoving()
    {
        enemyAnimation.StopMovementAnimation();
        enemyMovement.Stop();
    }

    /// <summary>
    /// 배정된 타깃으로 이동 경로를 갱신하고, 타깃이 해제되면 경로를 비운다.
    /// </summary>
    /// <param name="target">이동 목적지로 사용할 타깃. 해제된 경우 null.</param>
    protected override void OnTargetChanged(Transform target)
    {
        enemyMovement.SetDestination(target);
    }
}

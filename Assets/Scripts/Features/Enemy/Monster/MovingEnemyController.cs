using UnityEngine;

/// <summary>
/// 이동형 몬스터가 공유하는 목적지 설정과 이동·애니메이션 제어를 제공한다.
/// 이동할 시점과 구체적인 행동 흐름은 자식 클래스가 결정한다.
/// </summary>
public abstract class MovingEnemyController : EnemyController
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

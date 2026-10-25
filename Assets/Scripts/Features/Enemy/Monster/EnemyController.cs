using UnityEngine;

/// <summary>
/// 지정된 타겟에 따라 몬스터의 이동과 공격 행동을 조율한다.
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private EnemyTargeting  enemyTargeting;
    [SerializeField] private EnemyMovement enemyMovement;
    [SerializeField] private EnemyCombat enemyCombat;
    [SerializeField] private EnemyAnimation enemyAnimation;
    
    /// <summary>
    /// 다른 미사일이 이미 이 적을 목표로 삼았는지 나타낸다.
    /// 미사일 시스템은 중복 목표 배정을 막기 위해 이 값을 확인하고 설정한다.
    /// </summary>
    public bool IsTargeted { get; set; }
    
    /// <summary>
    /// 필요한 컴포넌트 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyMovement == null)
            enemyMovement = GetComponent<EnemyMovement>();
        if (enemyCombat == null)
            enemyCombat = GetComponent<EnemyCombat>();
        if (enemyAnimation == null)
            enemyAnimation = GetComponent<EnemyAnimation>();
    }

    private void LateUpdate()
    {
        Vector3 position = transform.position;
        position.z = position.y;
        transform.position = position;
    }

    /// <summary>
    /// 타겟과의 거리에 따라 이동하거나 공격한다.
    /// </summary>
    public void ExecuteBehavior()
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
    /// 몬스터가 추적할 타겟을 지정한다.
    /// </summary>
    /// <param name="target">추적할 타겟의 Transform.</param>
    public void SetTargetAndDestination(Transform target)
    {
        enemyTargeting.SetTarget(target);
        enemyMovement.SetDestination(target);
    }
    
    /// <summary>
    /// 지정된 방향으로 이동하고 이동 애니메이션 속도를 갱신한다.
    /// </summary>
    /// <param name="moveDirection">정규화된 이동 방향.</param>
    private void MoveTowardsTarget(Vector2 moveDirection)
    {
        enemyAnimation.PlayMovementAnimation(enemyMovement.MoveSpeed);
        enemyMovement.Move(moveDirection);
    }

    /// <summary>
    /// 몬스터 이동을 멈추고 대기 애니메이션으로 전환한다.
    /// </summary>
    private void StopMoving()
    {
        enemyAnimation.StopMovementAnimation();
        enemyMovement.Stop();
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

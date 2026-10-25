using UnityEngine;

/// <summary>
/// 지정된 타겟에 따라 몬스터의 이동과 공격 행동을 수행한다.
/// 타겟과의 거리를 기준으로 이동 또는 공격을 실행하고 애니메이션 상태를 갱신한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyController : MonoBehaviour
{
    // Animator 파라미터
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int MoveAnimSpeedHash = Animator.StringToHash("MoveAnimSpeed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [Header("참조")]
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyTargeting  enemyTargeting;
    [SerializeField] private EnemyMovement enemyMovement;
    [SerializeField] private EnemyCombat enemyCombat;
    
    
    [SerializeField] private float animationReferenceSpeed = 2f;
 
    
    // 런타임 상태
   
    
    
    private EnemyNetworkAnimator _networkAnimator;
    
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
        _networkAnimator = GetComponent<EnemyNetworkAnimator>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyMovement == null)
            enemyMovement = GetComponent<EnemyMovement>();
        if (enemyCombat == null)
            enemyCombat = GetComponent<EnemyCombat>();
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
                UpdateFacingDirection(attackDirection);

            TryAttack();
            return;
        }
        
        if (!enemyMovement.TryGetNextPathDirection(out Vector2 moveDirection))
        {
            StopMoving();
            return;
        }

        UpdateFacingDirection(moveDirection);
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
        // 실제 이동 속도를 기준으로 이동 애니메이션 재생 속도를 보정한다.
        animator.SetBool(IsMovingHash, true);

        float animationSpeed = enemyMovement.MoveSpeed / animationReferenceSpeed;
        animator.SetFloat(MoveAnimSpeedHash, animationSpeed);
        enemyMovement.Move(moveDirection);
    }

    /// <summary>
    /// 몬스터 이동을 멈추고 대기 애니메이션으로 전환한다.
    /// </summary>
    private void StopMoving()
    {
        animator.SetBool(IsMovingHash, false);
        enemyMovement.Stop();
    }

    /// <summary>
    /// 이동 방향을 상하좌우 애니메이션 방향으로 변환해 적용한다.
    /// </summary>
    /// <param name="moveDirection">현재 이동 방향.</param>
    private void UpdateFacingDirection(Vector2 moveDirection)
    {
        Vector2 animationDirection = GetAnimationDirection(moveDirection);
        
        animator.SetFloat(MoveXHash, animationDirection.x);
        animator.SetFloat(MoveYHash, animationDirection.y);
    }

    /// <summary>
    /// 연속적인 이동 방향을 상하좌우 네 방향 중 하나로 변환한다.
    /// </summary>
    /// <param name="direction">변환할 이동 방향.</param>
    /// <returns>상하좌우 중 하나로 정규화된 애니메이션 방향.</returns>
    private static Vector2 GetAnimationDirection(Vector2 direction)
    {
        // 대각선 방향은 더 큰 축을 기준으로 상하좌우 네 방향으로 변환한다.
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return new Vector2(Mathf.Sign(direction.x), 0f);

        return new Vector2(0f, Mathf.Sign(direction.y));
    }

    /// <summary>
    /// 공격 쿨타임을 확인한 뒤 네트워크 공격을 실행한다.
    /// </summary>
    private void TryAttack()
    {
        if (!enemyCombat.TryAttack())
            return;
        // Network Animator가 있으면 RPC로, 없으면 로컬 Animator로 공격을 재생한다.
        if (_networkAnimator != null)
            _networkAnimator.PlayAttack();
        else
            animator.SetTrigger(AttackHash);    
    }
}

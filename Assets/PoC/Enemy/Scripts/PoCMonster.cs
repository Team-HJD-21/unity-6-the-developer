using UnityEngine;

/// <summary>
/// <see cref="PoCAIBrain"/>이 선택한 타겟에 따라 실제 몬스터 행동을 수행한다.
/// 타겟과의 거리를 기준으로 이동 또는 공격을 실행하고 애니메이션 상태를 갱신한다.
/// </summary>
public class PoCMonster : MonoBehaviour
{
    // Animator 파라미터
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int MoveAnimSpeedHash = Animator.StringToHash("MoveAnimSpeed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [Header("참조")]
    [SerializeField] private Animator animator;

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float animationReferenceSpeed = 2f;

    [Header("공격 설정")]
    [SerializeField] private float attackRange = 0.4f;
    [SerializeField] private float attackCooldown = 1f;

    // 런타임 상태
    private Transform _target;
    private float _nextAttackTime;
    private PoCMonsterNetworkAnimator _networkAnimator;

    /// <summary>
    /// 필요한 컴포넌트 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
        _networkAnimator = GetComponent<PoCMonsterNetworkAnimator>();
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 타겟과의 거리에 따라 이동하거나 공격한다.
    /// </summary>
    public void ExecuteBehavior()
    {
        // 타겟이 없으면 이동을 멈추고 대기한다.
        if (_target == null)
        {
            StopMoving();
            return;
        }

        // 현재 위치에서 타겟까지의 이동 방향을 계산한다.
        Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
        Vector2 moveDirection = toTarget.normalized;

        // 이동 전 타겟 방향에 맞춰 바라보는 방향을 갱신한다.
        UpdateFacingDirection(moveDirection);

        // 공격 범위 안에서는 이동을 멈추고 공격을 시도한다.
        if (IsInAttackRange())
        {
            StopMoving();
            TryAttack();
            return;
        }

        // 공격 범위 밖에서는 타겟을 향해 이동한다.
        MoveTowardsTarget(moveDirection);
    }

    /// <summary>
    /// 몬스터가 추적할 타겟을 지정한다.
    /// </summary>
    public void SetTarget(Transform target)
    {
        _target = target;
    }

    /// <summary>
    /// 지정된 방향으로 이동하고 이동 애니메이션 속도를 갱신한다.
    /// </summary>
    private void MoveTowardsTarget(Vector2 moveDirection)
    {
        // 실제 이동 속도를 기준으로 이동 애니메이션 재생 속도를 보정한다.
        animator.SetBool(IsMovingHash, true);

        float animationSpeed = moveSpeed / animationReferenceSpeed;
        animator.SetFloat(MoveAnimSpeedHash, animationSpeed);

        // 계산한 방향과 속도로 몬스터 위치를 이동시킨다.
        transform.position += (Vector3)moveDirection * (moveSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 몬스터 이동을 멈추고 대기 애니메이션으로 전환한다.
    /// </summary>
    private void StopMoving()
    {
        animator.SetBool(IsMovingHash, false);
    }

    /// <summary>
    /// 이동 방향을 상하좌우 애니메이션 방향으로 변환해 적용한다.
    /// </summary>
    private void UpdateFacingDirection(Vector2 moveDirection)
    {
        Vector2 animationDirection = GetAnimationDirection(moveDirection);

        animator.SetFloat(MoveXHash, animationDirection.x);
        animator.SetFloat(MoveYHash, animationDirection.y);
    }

    /// <summary>
    /// 연속적인 이동 방향을 상하좌우 네 방향 중 하나로 변환한다.
    /// </summary>
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
        // 공격 쿨타임이 남아 있으면 이번 공격을 건너뛴다.
        if (Time.time < _nextAttackTime)
            return;

        _nextAttackTime = Time.time + attackCooldown;

        // Network Animator가 있으면 RPC로, 없으면 로컬 Animator로 공격을 재생한다.
        if (_networkAnimator != null)
            _networkAnimator.PlayAttack();
        else
            animator.SetTrigger(AttackHash);
    }

    /// <summary>
    /// 타겟이 공격 범위 안에 있는지 확인한다.
    /// </summary>
    private bool IsInAttackRange()
    {
        // 제곱 거리를 비교해 불필요한 제곱근 계산을 피한다.
        float distanceSqr = (_target.position - transform.position).sqrMagnitude;

        return distanceSqr <= attackRange * attackRange;
    }
}

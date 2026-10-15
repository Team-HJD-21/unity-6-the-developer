using UnityEngine;

/// <summary>
/// <see cref="EnemyAIBrain"/>이 선택한 타겟에 따라 실제 몬스터 행동을 수행한다.
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
    [SerializeField] private Rigidbody2D rigidbody2D;  
    
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float animationReferenceSpeed = 2f;
    [SerializeField] private float arrivalDistance = 0.4f;
    private Vector3[] _path;
    private bool _isPath = false;
    private int _currentPath = 0;
    private bool _isStopped = true;
    
    [Header("공격 설정")]
    [SerializeField] private float attackRange = 0.4f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = 5;
    
    // 런타임 상태
    private Transform _target;
    private float _nextAttackTime;
    private EnemyNetworkAnimator _networkAnimator;
    
    /// <summary>
    /// 다른 미사일이 이미 이 적을 목표로 삼았는지 나타낸다.
    /// 미사일 시스템은 중복 목표 배정을 막기 위해 이 값을 확인하고 설정한다.
    /// </summary>
    public bool IsTargeted { get; set; }
    
    // 렌더링
    private const int SortingScale = 100;
    [SerializeField] private Transform sortingOrigin;

    /// <summary>
    /// 필요한 컴포넌트 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
        _networkAnimator = GetComponent<EnemyNetworkAnimator>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if(rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
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
        if (_target == null)
        {
            StopMoving();
            return;
        }
        
        // 공격 범위 안에서는 이동을 멈추고 공격을 시도한다.
        if (IsInAttackRange())
        {
            StopMoving();
            TryAttack();
            return;
        }
        
        if (!TryGetNextPathDirection(out Vector2 moveDirection))
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
    public void SetTarget(Transform target)
    {
        _target = target;
        _path = null;
        _currentPath = 0;
        _isPath = false;
        
        if (target == null)
            return;
        
        Vector3 startLocation = this.transform.position;
        Vector3 arriveLocation = target.position;
        _isPath = EnemyNavMeshPathfinder.TryCalculatePath(startLocation, arriveLocation, out _path);
    }

    private bool TryGetNextPathDirection(out Vector2 direction)
    {
        direction = Vector2.zero;

        if (!_isPath || _path == null)
            return false;

        // 이미 도착한 지점은 건너뛴다.
        while (_currentPath < _path.Length &&
               Vector2.Distance(transform.position, _path[_currentPath]) <= arrivalDistance)
        {
            _currentPath++;
        }

        if (_currentPath >= _path.Length)
            return false;

        direction = ((Vector2)_path[_currentPath] -
                     (Vector2)transform.position).normalized;
        return true;
    }
    
    /// <summary>
    /// 지정된 방향으로 이동하고 이동 애니메이션 속도를 갱신한다.
    /// </summary>
    /// <param name="moveDirection">정규화된 이동 방향.</param>
    private void MoveTowardsTarget(Vector2 moveDirection)
    {
        if (_isPath)
        {
            // 실제 이동 속도를 기준으로 이동 애니메이션 재생 속도를 보정한다.
            animator.SetBool(IsMovingHash, true);

            float animationSpeed = moveSpeed / animationReferenceSpeed;
            animator.SetFloat(MoveAnimSpeedHash, animationSpeed);

            // 계산한 방향과 속도로 몬스터 위치를 이동시킨다.
            _isStopped = false;
            rigidbody2D.linearVelocity = moveDirection * moveSpeed;
        }
    }

    /// <summary>
    /// 몬스터 이동을 멈추고 대기 애니메이션으로 전환한다.
    /// </summary>
    private void StopMoving()
    {
        animator.SetBool(IsMovingHash, false);

        if (_isStopped)
            return;

        rigidbody2D.linearVelocity = Vector2.zero;
        _isStopped = true;
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
        // 공격 쿨타임이 남아 있으면 이번 공격을 건너뛴다.
        if (Time.time < _nextAttackTime)
            return;

        _nextAttackTime = Time.time + attackCooldown;

        if (_target == null)
        {
            return;
        }


        TargetableComponent targetable = _target.GetComponent<TargetableComponent>();
        if (targetable == null)
            return;

        if (targetable.TargetType == TargetType.Turret)
        {
            targetable.TryApplyDamage(attackDamage);
        }
        else if (targetable.TargetType == TargetType.Player)
        {
            PlayerInfo player = _target.GetComponentInParent<PlayerInfo>();
            if (player != null)
                player.TakeDamage(attackDamage);
        }
        
        // Network Animator가 있으면 RPC로, 없으면 로컬 Animator로 공격을 재생한다.
        if (_networkAnimator != null)
            _networkAnimator.PlayAttack();
        else
            animator.SetTrigger(AttackHash);    
    }

    /// <summary>
    /// 타겟이 공격 범위 안에 있는지 확인한다.
    /// </summary>
    /// <returns>타겟이 공격 범위 안에 있으면 <see langword="true"/>.</returns>
    private bool IsInAttackRange()
    {
        // 제곱 거리를 비교해 불필요한 제곱근 계산을 피한다.
        float distanceSqr =
            ((Vector2)_target.position - (Vector2)transform.position).sqrMagnitude;

        return distanceSqr <= attackRange * attackRange;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_path == null)
            return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < _path.Length; i++)
        {
            Vector3 point = _path[i];
            point.z = transform.position.z;
            Gizmos.DrawSphere(point, 0.15f);

            if (i > 0)
            {
                Vector3 previous = _path[i - 1];
                previous.z = transform.position.z;
                Gizmos.DrawLine(previous, point);
            }
        }
    }
}

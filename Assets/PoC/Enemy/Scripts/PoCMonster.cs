using UnityEngine;

/// <summary>
/// 몬스터의 타겟 추적, 이동, 공격 판단과 애니메이션을 처리한다.
/// 네트워크 환경에서는 서버가 <see cref="Tick"/>을 호출한다.
/// </summary>
public class PoCMonster : MonoBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int MoveAnimSpeedHash = Animator.StringToHash("MoveAnimSpeed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float animationReferenceSpeed = 2f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 0.4f;
    [SerializeField] private float attackCooldown = 1f;

    private Transform _target;
    private float _nextAttackTime;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 타겟과의 거리에 따라 이동하거나 공격한다.
    /// </summary>
    public void Tick()
    {
        if (_target == null)
            return;

        Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
        Vector2 moveDirection = toTarget.normalized;

        UpdateFacingDirection(moveDirection);

        if (IsInAttackRange())
        {
            StopMoving();
            TryAttack();
            return;
        }

        MoveTowardsTarget(moveDirection);
    }

    /// <summary>
    /// 몬스터가 추적할 타겟을 지정한다.
    /// </summary>
    public void SetTarget(Transform target)
    {
        _target = target;
    }

    private void MoveTowardsTarget(Vector2 moveDirection)
    {
        animator.SetBool(IsMovingHash, true);

        float animationSpeed = moveSpeed / animationReferenceSpeed;
        animator.SetFloat(MoveAnimSpeedHash, animationSpeed);

        transform.position += (Vector3)moveDirection * (moveSpeed * Time.deltaTime);
    }

    private void StopMoving()
    {
        animator.SetBool(IsMovingHash, false);
    }

    private void UpdateFacingDirection(Vector2 moveDirection)
    {
        Vector2 animationDirection = GetAnimationDirection(moveDirection);

        animator.SetFloat(MoveXHash, animationDirection.x);
        animator.SetFloat(MoveYHash, animationDirection.y);
    }

    private static Vector2 GetAnimationDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return new Vector2(Mathf.Sign(direction.x), 0f);

        return new Vector2(0f, Mathf.Sign(direction.y));
    }

    private void TryAttack()
    {
        if (Time.time < _nextAttackTime)
            return;

        _nextAttackTime = Time.time + attackCooldown;
        animator.SetTrigger(AttackHash);
    }

    private bool IsInAttackRange()
    {
        float distanceSqr = (_target.position - transform.position).sqrMagnitude;

        return distanceSqr <= attackRange * attackRange;
    }
}

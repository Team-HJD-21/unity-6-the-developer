using System;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float animationReferenceSpeed = 2f;
    private EnemyNetworkAnimator _networkAnimator;
    
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int MoveAnimSpeedHash = Animator.StringToHash("MoveAnimSpeed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    
    private void Awake()
    {
        _networkAnimator = GetComponent<EnemyNetworkAnimator>();

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 이동 방향을 상하좌우 애니메이션 방향으로 변환해 적용한다.
    /// </summary>
    /// <param name="moveDirection">현재 이동 방향.</param>
    public void SetFacingDirection(Vector2 moveDirection)
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

    public void PlayMovementAnimation(float moveSpeed)
    {
        // 실제 이동 속도를 기준으로 이동 애니메이션 재생 속도를 보정한다.
        animator.SetBool(IsMovingHash, true);

        float animationSpeed = moveSpeed / animationReferenceSpeed;
        animator.SetFloat(MoveAnimSpeedHash, animationSpeed);
    }

    public void StopMovementAnimation()
    {
        animator.SetBool(IsMovingHash, false);
    }

    /// <summary>
    /// 기존 Animator의 Attack 슬롯으로 능력 발동 애니메이션을 재생한다.
    /// </summary>
    public void PlayAbilityAnimation()
    {
        if (_networkAnimator != null && _networkAnimator.IsSpawned)
            _networkAnimator.PlayAttack();
        else
            animator.SetTrigger(AttackHash);
    }
}

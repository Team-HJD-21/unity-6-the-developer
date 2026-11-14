using System;
using UnityEngine;

/// <summary>
/// 방향과 이동 모션을 갱신하고 로컬 또는 네트워크 경로로 능력 모션을 재생한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float animationReferenceSpeed = 2f;
    private EnemyNetworkAnimator _networkAnimator;
    public TeamHJD.Game.Presentation.GameAudioCue AbilitySound;
    
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int MoveAnimSpeedHash = Animator.StringToHash("MoveAnimSpeed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    
    private Vector2 _lastDirection;
    private bool _lastIsMoving;
    private float _lastAnimationSpeed;
    
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

        if (_lastDirection == animationDirection)
            return;

        animator.SetFloat(MoveXHash, animationDirection.x);
        animator.SetFloat(MoveYHash, animationDirection.y);

        _lastDirection = animationDirection;
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
    /// 이동 모션을 켜고 실제 이동 속도에 맞춰 애니메이션 재생 속도를 보정한다.
    /// </summary>
    public void PlayMovementAnimation(float moveSpeed)
    {
        SetMoving(true);

        float animationSpeed = moveSpeed / animationReferenceSpeed;

        if (_lastAnimationSpeed.Equals(animationSpeed))
            return;

        animator.SetFloat(MoveAnimSpeedHash, animationSpeed);
        _lastAnimationSpeed = animationSpeed;
    }

    /// <summary>
    /// 바라보는 방향을 유지한 채 이동 모션을 끈다.
    /// </summary>
    public void StopMovementAnimation()
    {
        SetMoving(false);
    }

    /// <summary>이동 상태가 바뀐 경우만 Animator에 전달한다.</summary>
    private void SetMoving(bool isMoving)
    {
        if (_lastIsMoving == isMoving)
            return;

        animator.SetBool(IsMovingHash, isMoving);
        _lastIsMoving = isMoving;
    }

    /// <summary>
    /// 기존 Animator의 Attack 슬롯으로 능력 발동 애니메이션을 재생한다.
    /// </summary>
    public void PlayAbilityAnimation()
    {
        if (_networkAnimator != null && _networkAnimator.IsSpawned)
            _networkAnimator.PlayAttack();
        else
        {
            animator.SetTrigger(AttackHash);
            PlayAbilitySound();
        }
    }
    /// <summary>기존 로컬/수신 애니메이션 경로에서 한 번 호출합니다. 공간 처리는 표준 AudioSource가 담당합니다.</summary>
    public void PlayAbilitySound() => TeamHJD.Game.Presentation.GameAudioManager.Play(AbilitySound, this);
    
    private void OnEnable()
    {
        RefreshStateCache();
    }

    /// <summary>
    /// 실제 Animator 값과 캐시를 맞춘다.
    /// 초기화와 저장 복원 직후 호출한다.
    /// </summary>
    internal void RefreshStateCache()
    {
        _lastDirection = new Vector2(
            animator.GetFloat(MoveXHash),
            animator.GetFloat(MoveYHash));

        _lastIsMoving = animator.GetBool(IsMovingHash);
        _lastAnimationSpeed = animator.GetFloat(MoveAnimSpeedHash);
    }
}

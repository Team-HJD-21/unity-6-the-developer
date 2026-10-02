using UnityEngine;

/// <summary>
/// 플레이어 외형 렌더링, 마우스 조준 방향에 따른 스프라이트 반전 및 애니메이션 상태 제어 컴포넌트
/// 
/// [채택 이유 및 대안 비교]
/// 1. 원본 Animator 파라미터(isSide, isIdle, isUp - Bool 방식) 완전 복원:
///    - 대안: Animator.SetFloat("MoveSpeed", speed) 형태의 블렌드 트리 방식.
///    - 채택 이유: 프로젝트의 실제 플레이어 애니메이터 컨트롤러에는 Float 파라미터가 없고
///      마우스 각도 범위(-45~45도, 45~135도, -135~-45도, 기타)에 따라 
///      isSide, isIdle, isUp 세 개의 Bool 플래그를 켜고 끄는 FSM(유한 상태 기계)으로 설계되어 있습니다.
///      기존 에셋 구조를 그대로 유지하여 'Parameter Does not exist' 에러를 근본적으로 해결했습니다.
/// 
/// 2. Animator.StringToHash() 캐싱 적용:
///    - 대안: animator.SetBool("isSide", true) 등 매 프레임 문자열 직접 전달.
///    - 채택 이유: 문자열 비교는 내부적으로 해시 변환 비용과 GC를 유발할 수 있습니다.
///      Awake에서 해시 ID를 미리 캐싱(isSideHash 등)해두면 모바일이나 저사양 환경에서도
///      가장 가볍고 정밀하게 애니메이션 상태를 갱신할 수 있습니다.
/// 
/// 3. InGameManager 상태 검사 (웨이브 활성화, 대화 및 일시정지 보호):
///    - 대안: Update 내부에서 항상 마우스 각도를 계산하여 즉각 회전.
///    - 채택 이유: 게임 시작 직후 포탑 설치/활성화 준비 단계에서는 웨이브가 시작되지 않은 상태(!isWave)이므로,
///      마우스를 움직여도 캐릭터가 회전하지 않고 기본 정면(Idle)을 유지해야 합니다.
///      웨이브 시작 버튼을 눌러 isWave가 true가 된 이후에만 마우스 추적 조준을 활성화하도록 보호합니다.
/// </summary>
public class PlayerVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    // 문자열 검색 오버헤드를 없애기 위한 Animator 파라미터 해시 캐싱
    private static readonly int IsSideHash = Animator.StringToHash("isSide");
    private static readonly int IsIdleHash = Animator.StringToHash("isIdle");
    private static readonly int IsUpHash = Animator.StringToHash("isUp");

    private void Reset()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // 게임 시작 시 기본 방향: 정면 대기(Idle), 반전 없음
        SetAnimationState(side: false, idle: true, up: false);
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = false;
        }
    }

    private void Update()
    {
        if (inputHandler == null || spriteRenderer == null) return;

        // 인게임 매니저 상태 검사: 대화 중, 일시정지 중이거나 웨이브 시작 전(!isWave)일 때는 조준 갱신 차단
        if (!CanAimInCurrentState()) return;

        UpdateVisualAndAnimation();
    }

    /// <summary>
    /// 현재 인게임 상태를 확인하여 마우스 조준 회전 허용 여부를 반환
    /// </summary>
    private bool CanAimInCurrentState()
    {
        if (!GeneralManager.TryGetExistingInstance(out var manager) || manager.inGameManager == null) return true;

        var igm = manager.inGameManager;

        // 대화 중이거나 일시정지 창이 떠 있는 경우 차단
        if (igm.isTalking || igm.pauseVisible) return false;

        // 포탑 설치 단계 등 웨이브가 정식 시작되지 않았을 때 차단
        if (!igm.isWave) return false;

        return true;
    }

    /// <summary>
    /// 마우스 조준 방향 벡터의 각도를 계산하여 스프라이트 반전 및 애니메이션 파라미터(isSide, isIdle, isUp)를 갱신
    /// </summary>
    private void UpdateVisualAndAnimation()
    {
        Vector2 aimDir = inputHandler.AimDirection;
        if (aimDir.sqrMagnitude < 0.001f) return;

        // 조준 각도(-180 ~ 180도) 산출
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

        // 레거시 Player.cs의 원본 각도 분기 로직 완벽 복원
        if (angle > -45f && angle <= 45f)
        {
            // 우측 조준: 측면 애니메이션 + 우측 바라봄(flipX = true)
            SetAnimationState(side: true, idle: false, up: false);
            spriteRenderer.flipX = true;
        }
        else if (angle > 45f && angle <= 135f)
        {
            // 상단 조준: 위쪽 애니메이션 (isUp)
            SetAnimationState(side: false, idle: false, up: true);
            spriteRenderer.flipX = false;
        }
        else if (angle > -135f && angle <= -45f)
        {
            // 하단 조준: 정면/대기 애니메이션 (isIdle)
            SetAnimationState(side: false, idle: true, up: false);
            spriteRenderer.flipX = false;
        }
        else
        {
            // 좌측 조준: 측면 애니메이션 + 좌측 바라봄(flipX = false)
            SetAnimationState(side: true, idle: false, up: false);
            spriteRenderer.flipX = false;
        }
    }

    /// <summary>
    /// 3개의 Bool 파라미터를 해시를 통해 원자적으로 일괄 갱신
    /// </summary>
    private void SetAnimationState(bool side, bool idle, bool up)
    {
        if (animator == null) return;

        animator.SetBool(IsSideHash, side);
        animator.SetBool(IsIdleHash, idle);
        animator.SetBool(IsUpHash, up);
    }
}

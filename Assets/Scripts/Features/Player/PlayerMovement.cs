// Rigidbody2D 속도로 Player 이동을 제어합니다.
// 입력·사망·스킬 선택의 잠금은 따르되 필드 공격과 우주선 내부 이동을 구분합니다.
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("캐릭터 이동 속도")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerInputHandler inputHandler;
    private PlayerSkillController _skills;
    private PlayerControlState _controls;

    // 대화 중이거나 사망 시 외부(매니저, UI, 컷씬 등)에서 이동을 멈출 수 있는 스위치
    public bool CanMove { get; set; } = true;

    // 인스펙터에서 컴포넌트를 붙이거나 추가할 때 자동 연결해 주는 편의 기능
    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Awake()
    {
        _skills = GetComponent<PlayerSkillController>();
        _controls = GetComponent<PlayerControlState>();
        // 인스펙터 할당을 깜빡했을 때를 대비한 널 세이프 방어 코드
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();

        // 물리 연산 시 Z축 회전(캐릭터가 벽에 부딪혀 팽이처럼 도는 현상) 방지
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
    }

    // 물리 이동은 프레임 레이트와 상관없이 일정한 물리 주기로 도는 FixedUpdate에서 처리
    private void FixedUpdate()
    {
        // 이동 불가 상태이거나 입력 컴포넌트가 없으면 즉시 정지
        _controls ??= GetComponent<PlayerControlState>();
        if (!CanMove || inputHandler == null || (_controls != null && !_controls.CanAct) ||
            (_skills != null && _skills.IsSelectionOpen))
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 입력받은 정규화 방향 벡터 * 속도로 물리 속도 부여
        rb.linearVelocity = inputHandler.MoveInput * moveSpeed;
    }

    /// <summary>
    /// 피격, 기절, 넉백 등 즉시 이동을 멈춰야 할 때 외부에서 호출
    /// </summary>
    public void StopImmediately()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}

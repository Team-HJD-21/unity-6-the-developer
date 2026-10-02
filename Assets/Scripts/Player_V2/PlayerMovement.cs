using UnityEngine;

/// <summary>
/// Rigidbody2D 기반 순수 물리 이동 컴포넌트
/// 
/// [채택 이유 및 대안 비교]
/// 1. Rigidbody2D.linearVelocity(속도 대입) vs Transform.position 누적:
///    - 대안: transform.position += moveInput * speed * Time.deltaTime (레거시 Player.cs 방식).
///    - 채택 이유: Transform을 강제로 옮기면 타일맵/콜라이더 벽면 충돌 시 캐릭터가 벽 속으로 파고들었다가
///      밀려나는 미세 떨림(Jitter)이 발생합니다. 또한 물리 콜라이더 연산이 꼬이며, 향후 NetworkTransform 동기화 시 오차가 커집니다.
///      반면 물리 속도를 제어하면 유니티 물리 엔진이 벽면 미끄러짐과 충돌 처리를 완벽하게 보장합니다.
/// 
/// 2. Rigidbody2D.AddForce vs linearVelocity:
///    - 대안: AddForce (가속도 누적).
///    - 채택 이유: 우주선 관성 비행이 아닌 탑다운 캐릭터 조작이므로, 키를 뗐을 때 즉시 멈추고
///      눌렀을 때 즉시 목표 속도에 도달해야 하므로 velocity 직접 지정이 조작감에 가장 적합합니다.
/// 
/// 3. GeneralManager 의존성 제거:
///    - 대안: InGameManager.Instance.canMove 검사.
///    - 채택 이유: 싱글톤 참조를 끊어야 매니저가 없는 독립 테스트 씬(PlayerTest.unity)에서도 단독 검증이 가능합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("캐릭터 이동 속도")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerInputHandler inputHandler;

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
        if (GeneralManager.TryGetExistingInstance(out var manager) && manager.inGameManager != null)
        {
            var igm = manager.inGameManager;
            if (igm.isTalking || !igm.isWave)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
        }
        // 이동 불가 상태이거나 입력 컴포넌트가 없으면 즉시 정지
        if (!CanMove || inputHandler == null)
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

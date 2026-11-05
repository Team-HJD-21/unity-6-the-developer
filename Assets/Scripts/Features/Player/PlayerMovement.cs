// Player의 이동/대시 속도를 적용하고 입력·생존·필드 정지 경계를 반영합니다.
// 허브 이동은 필드 행동과 분리하며 Rigidbody 속도는 이 컴포넌트만 변경합니다.
using UnityEngine;
using TeamHJD.Game.Presentation.Scene;

/// <summary>일반 이동과 대시의 물리 속도를 한 곳에서 적용하고 조작 차단 시 즉시 정지합니다.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("캐릭터 이동 속도")]
    [SerializeField, Min(0)] private float moveSpeed = 5f;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerInputHandler inputHandler;
    private PlayerControlState _controls;
    private PlayerDash _dash;
    private PlayerSlowEffects _slowEffects;

    public float MoveSpeed => float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed) ? 0 : Mathf.Max(0, moveSpeed);
    public bool CanAcceptInput => isActiveAndEnabled && CanMove && Time.timeScale > 0 && rb != null &&
        inputHandler != null && inputHandler.isActiveAndEnabled &&
        (_controls == null || _controls.CanMove);

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
        _controls = GetComponent<PlayerControlState>();
        _dash = GetComponent<PlayerDash>();
        // 기존 제작 prefab도 같은 감속 수신/저장 원본을 빠짐없이 갖게 합니다.
        _slowEffects = GetComponent<PlayerSlowEffects>() ?? gameObject.AddComponent<PlayerSlowEffects>();
        // 인스펙터 할당을 깜빡했을 때를 대비한 널 세이프 방어 코드
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (inputHandler == null)
            inputHandler = GetComponent<PlayerInputHandler>();

        // 물리 연산 시 Z축 회전(캐릭터가 벽에 부딪혀 팽이처럼 도는 현상) 방지
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
    }

    // 일시정지 중에는 FixedUpdate가 멈추므로 Update에서도 중단 상태를 정리합니다.
    private void Update()
    {
        if (!CanAcceptInput)
            HoldOrStop();
    }

    // 일반 이동과 대시가 서로 속도를 덮어쓰지 않도록 한 곳에서만 적용합니다.
    private void FixedUpdate()
    {
        if (!CanAcceptInput)
        {
            HoldOrStop();
            return;
        }
        float step = Time.fixedDeltaTime;
        var presence = GetComponent<PlayerMatchPresence>();
        if (presence != null && presence.Location == TeamHJD.Game.Domain.MatchPlayerLocation.Field)
            step = FieldSimulationTime.FixedDeltaTime(this);
        Vector2 velocity = _dash != null && step > 0 && _dash.TryGetVelocity(step, out var dashVelocity)
            ? dashVelocity : inputHandler.MoveInput * MoveSpeed;
        rb.linearVelocity = velocity * _slowEffects.SpeedMultiplier * (Time.fixedDeltaTime > 0 ? step / Time.fixedDeltaTime : 0f);
    }

    private void HoldOrStop()
    {
        // 필드 정지는 대시의 남은 상태를 지우지 않습니다. 사망/메뉴/외부 이동 잠금은 기존 중단을 유지합니다.
        if (CanMove && _controls != null && _controls.IsFieldPaused && !_controls.IsBlocked)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }
        StopImmediately();
    }

    /// <summary>
    /// 피격, 기절, 넉백 등 즉시 이동을 멈춰야 할 때 외부에서 호출
    /// </summary>
    public void StopImmediately()
    {
        if (_dash != null)
            _dash.CancelDash();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void OnDisable() => StopImmediately();
}

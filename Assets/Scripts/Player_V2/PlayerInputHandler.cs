using UnityEngine;

/// <summary>
/// 플레이어 조작 입력(WASD, 조준, 공격, 폭탄, 스킬, 무기 교체 등) 수집 전담 컴포넌트
/// 
/// [채택 이유 및 대안 비교]
/// 1. 단일 책임 입력 수집 허브:
///    - 대안: 각 스크립트에서 Input.GetKeyDown을 직접 수집.
///    - 채택 이유: 입력 창구를 한곳으로 모아 일시정지, 대화 등의 상황에서 일괄 제어가 가능하며,
///      스킬 키 바인딩 변경 시 PlayerInputHandler만 수정하면 됩니다.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PlayerInputHandler : MonoBehaviour
{

    // [이동 및 조준]
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimDirection { get; private set; } = Vector2.down;
    public Vector2 MouseWorldPosition { get; private set; }
    public bool HasAimTarget { get; private set; }

    // [기본 사격 (Space)]
    public bool IsAttackPressed { get; private set; }

    // [E 슬롯 입력 — 선택된 스킬은 PlayerSkillController가 결정]
    public bool WasBombPressed { get; private set; }

    // [Q 슬롯 입력 — 선택된 스킬은 PlayerSkillController가 결정]
    public bool WasSkillQPressed { get; private set; }

    public bool WasRecoverPressed { get; private set; }

    // [무기 교체 (Tab)]
    public bool WasWeaponSwitchPressed { get; private set; }

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
        MouseWorldPosition = transform.position;
    }

    private void Update()
    {
        // 1. 이동 방향 (WASD 및 화살표)
        MoveInput = GameInput.Move;

        // 2. 마우스 조준 방향 및 월드 좌표
        HasAimTarget = false;
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            var aimPlane = new Plane(Vector3.forward, transform.position);
            Ray pointerRay = _mainCamera.ScreenPointToRay(GameInput.PointerPosition);
            if (aimPlane.Raycast(pointerRay, out float distance))
            {
                HasAimTarget = true;
                MouseWorldPosition = pointerRay.GetPoint(distance);
                Vector2 direction = MouseWorldPosition - (Vector2)transform.position;
                if (PlayerAimUtility.IsValidDirection(direction)) AimDirection = direction.normalized;
            }
        }

        // 3. 공격 입력 (Space 연사 지속)
        IsAttackPressed = GameInput.IsPressed(GameKey.Space);

        // 4. E 슬롯 입력 (단발)
        WasBombPressed = GameInput.WasPressedThisFrame(GameKey.E);

        // 5. Q 슬롯 입력 (단발)
        WasSkillQPressed = GameInput.WasPressedThisFrame(GameKey.Q);
        WasRecoverPressed = GameInput.WasPressedThisFrame(GameKey.F);

        // 6. 무기 교체 입력 (Tab 키 감지)
        WasWeaponSwitchPressed = GameInput.WasPressedThisFrame(GameKey.Tab);
    }
}

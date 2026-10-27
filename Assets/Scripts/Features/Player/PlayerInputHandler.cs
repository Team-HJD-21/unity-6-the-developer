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

    // [기본 공격 (LMB): UI·설치·포탑 선택 뒤 PlayerAttack이 최종 판정]
    public bool IsAttackPressed { get; private set; }

    // [E 슬롯 입력 — 선택된 스킬은 PlayerSkillController가 결정]
    public bool WasBombPressed { get; private set; }

    // [Q 슬롯 입력 — 선택된 스킬은 PlayerSkillController가 결정]
    public bool WasSkillQPressed { get; private set; }

    public bool WasRecoverPressed { get; private set; }

    // [무기 교체 (Tab)]
    public bool WasWeaponSwitchPressed { get; private set; }
    public int RequestedWeaponSlot { get; private set; }
    public int WeaponScrollStep { get; private set; }

    private Camera _mainCamera;
    private PlayerSkillController _skills;
    private readonly PlayerAttackGesture _attackGesture = new();

    private void Awake()
    {
        _mainCamera = Camera.main;
        _skills = GetComponent<PlayerSkillController>();
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

        // UI가 클릭으로 닫히거나 설치가 취소되기 전의 입력 문맥을 보존합니다.
        IsAttackPressed = false;
        if (GameInput.PrimaryPointerWasPressedThisFrame && GameInput.IsPointerOverUi())
            GameInput.TryConsumePrimaryPointerPress();
        if (_skills != null && _skills.IsPlacingSkill)
            GameInput.ReservePrimaryPointerForPlacement();

        // 4. E 슬롯 입력 (단발)
        WasBombPressed = GameInput.WasPressedThisFrame(GameKey.E);

        // 5. Q 슬롯 입력 (단발)
        WasSkillQPressed = GameInput.WasPressedThisFrame(GameKey.Q);
        WasRecoverPressed = GameInput.WasPressedThisFrame(GameKey.F);

        // 6. 무기 교체 입력 (Tab 키 감지)
        WasWeaponSwitchPressed = GameInput.WasPressedThisFrame(GameKey.Tab);
        ReadWeaponSelection();
    }

    private void ReadWeaponSelection()
    {
        RequestedWeaponSlot = 0;
        if (GameInput.WasPressedThisFrame(GameKey.Digit1) || GameInput.WasPressedThisFrame(GameKey.Numpad1)) RequestedWeaponSlot = 1;
        else if (GameInput.WasPressedThisFrame(GameKey.Digit2) || GameInput.WasPressedThisFrame(GameKey.Numpad2)) RequestedWeaponSlot = 2;
        else if (GameInput.WasPressedThisFrame(GameKey.Digit3) || GameInput.WasPressedThisFrame(GameKey.Numpad3)) RequestedWeaponSlot = 3;
        else if (GameInput.WasPressedThisFrame(GameKey.Digit4) || GameInput.WasPressedThisFrame(GameKey.Numpad4)) RequestedWeaponSlot = 4;

        bool usesDebugCamera = GameInput.IsPressed(GameKey.LeftShift) || GameInput.IsPressed(GameKey.RightShift);
        bool overUi = GameInput.ScrollY != 0 && GameInput.IsPointerOverUi();
        WeaponScrollStep = usesDebugCamera || overUi ? 0 : -(int)GameInput.ScrollY;
    }

    public bool ResolvePrimaryAttack(bool canAttack)
    {
        bool blocked = !isActiveAndEnabled || !canAttack || GameInput.PrimaryPointerWasConsumed ||
            GameInput.PrimaryPointerReservedForPlacement ||
            (GameInput.PrimaryPointerIsPressed && GameInput.IsPointerOverUi());
        IsAttackPressed = _attackGesture.Read(GameInput.PrimaryPointerIsPressed,
            GameInput.PrimaryPointerWasPressedThisFrame, blocked);
        return IsAttackPressed;
    }

    public void CancelAttackGesture()
    {
        _attackGesture.Reset();
        IsAttackPressed = false;
    }

    private void OnDisable() => CancelAttackGesture();
}

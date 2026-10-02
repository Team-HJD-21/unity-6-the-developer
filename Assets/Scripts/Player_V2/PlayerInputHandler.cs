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
public class PlayerInputHandler : MonoBehaviour
{
    // [이동 및 조준]
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimDirection { get; private set; }
    public Vector2 MouseWorldPosition { get; private set; }

    // [기본 사격 (Space)]
    public bool IsAttackPressed { get; private set; }

    // [폭탄 설치 (E)]
    public bool WasBombPressed { get; private set; }

    // [보조 포탑 스킬 (Q)]
    public bool WasSkillQPressed { get; private set; }

    // [무기 교체 (Tab)]
    public bool WasWeaponSwitchPressed { get; private set; }

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // 1. 이동 방향 (WASD 및 화살표)
        Vector2 move = Vector2.zero;
        if (GameInput.IsPressed(GameKey.W) || GameInput.IsPressed(GameKey.UpArrow)) move.y += 1f;
        if (GameInput.IsPressed(GameKey.S) || GameInput.IsPressed(GameKey.DownArrow)) move.y -= 1f;
        if (GameInput.IsPressed(GameKey.A) || GameInput.IsPressed(GameKey.LeftArrow)) move.x -= 1f;
        if (GameInput.IsPressed(GameKey.D) || GameInput.IsPressed(GameKey.RightArrow)) move.x += 1f;
        MoveInput = move.normalized;

        // 2. 마우스 조준 방향 및 월드 좌표
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 screenPos = GameInput.PointerPosition;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
            worldPos.z = 0f;
            MouseWorldPosition = worldPos;

            Vector2 dir = (MouseWorldPosition - (Vector2)transform.position);
            AimDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        }

        // 3. 공격 입력 (Space 연사 지속)
        IsAttackPressed = GameInput.IsPressed(GameKey.Space);

        // 4. 폭탄 설치 입력 (E 단발)
        WasBombPressed = GameInput.WasPressedThisFrame(GameKey.E);

        // 5. 보조 포탑 스킬 입력 (Q 단발)
        WasSkillQPressed = Input.GetKeyDown(KeyCode.Q);

        // 6. 무기 교체 입력 (Tab 키 감지)
        WasWeaponSwitchPressed = Input.GetKeyDown(KeyCode.Tab);
    }
}
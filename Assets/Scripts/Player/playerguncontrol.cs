using UnityEngine;

/// <summary>
/// 마우스 조준 방향에 따라 4방향(상/하/좌/우) 총기 오브젝트의 위치와 활성화를 제어하는 컴포넌트다.
/// 메인 씬에서는 웨이브가 활성화(isWave == true)된 이후에만 조준이 동작하며,
/// 웨이브 시작 전이나 대화/일시정지 중에는 기본 총기(하단)만 표시하고 조준을 차단한다.
/// 매니저가 없는 테스트 씬에서는 항상 조준이 가능하다.
/// </summary>
public class PlayerGunControl : MonoBehaviour
{
    [Header("Gun Transforms")]
    [SerializeField] private Transform GunsideL;
    [SerializeField] private Transform GunsideR;
    [SerializeField] private Transform Gunup;
    [SerializeField] private Transform Gundown;

    [Header("Player Target Point")]
    [SerializeField] private Transform Playerpoint;

    private Camera _mainCamera;

    private void Start()
    {
        // 씬 내의 메인 카메라 캐싱
        CacheCamera();

        // 게임 시작 직후 4개 총기가 동시에 노출되는 현상 방지
        ResetToDefaultGun();
    }

    private void Update()
    {
        // 1. 인게임 매니저 상태 검사 (웨이브 시작 전, 대화 중, 일시정지 중이면 조준 차단)
        if (!CanControlGunInCurrentState())
        {
            ResetToDefaultGun();
            return;
        }

        // 2. 카메라 참조 안전 검사
        if (_mainCamera == null)
        {
            CacheCamera();
            if (_mainCamera == null) return;
        }

        // 3. 조준 및 총 위치/활성화 갱신 실행
        UpdateGunPosition();
    }

    /// <summary>
    /// 메인 씬에서는 웨이브 활성화 여부(isWave) 및 대화/일시정지 상태를 검사하고,
    /// 매니저가 없는 독립 테스트 씬에서는 항상 true를 반환합니다.
    /// </summary>
    private bool CanControlGunInCurrentState()
    {
        // GeneralManager 또는 InGameManager가 없는 테스트 씬 환경이면 항상 조준 허용
        if (!GeneralManager.TryGetExistingInstance(out var manager) || manager.inGameManager == null)
        {
            return true;
        }

        var igm = manager.inGameManager;

        // 메인 씬: 웨이브가 시작되지 않았거나(!isWave), 대화 중이거나, 일시정지 메뉴가 열려 있으면 조준 차단
        if (!igm.isWave || igm.isTalking || igm.pauseVisible)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 마우스 포인터 방향에 따라 상/하/좌/우 총 오브젝트를 활성화합니다.
    /// </summary>
    private void UpdateGunPosition()
    {
        Vector3 originPos = (Playerpoint != null) ? Playerpoint.position : transform.position;

        // 마우스 월드 좌표 및 방향 벡터 계산
        Vector3 mouseScreenPos = Input.mousePosition;
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2 aimDirection = (mouseWorldPos - originPos).normalized;

        if (aimDirection.sqrMagnitude < 0.001f)
            return;

        // 각도 계산 (-180 ~ 180도)
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        // 방향 판정: 우( -45 ~ 45 ), 상( 45 ~ 135 ), 좌( 135 ~ 180 / -180 ~ -135 ), 하( -135 ~ -45 )
        bool isRight = angle >= -45f && angle <= 45f;
        bool isUp = angle > 45f && angle <= 135f;
        bool isDown = angle >= -135f && angle < -45f;
        bool isLeft = !isRight && !isUp && !isDown;

        // 방향별 오브젝트 활성화/비활성화 일괄 적용
        ApplyGunVisibility(isRight, isLeft, isUp, isDown);
    }

    /// <summary>
    /// 조준이 비활성화되었을 때 아래쪽 총기만 켜고 나머지는 정리합니다.
    /// </summary>
    private void ResetToDefaultGun()
    {
        ApplyGunVisibility(right: false, left: false, up: false, down: true);
    }

    /// <summary>
    /// 4개 총기 오브젝트의 Active 상태를 일괄 갱신합니다.
    /// </summary>
    private void ApplyGunVisibility(bool right, bool left, bool up, bool down)
    {
        if (GunsideR != null && GunsideR.gameObject.activeSelf != right)
            GunsideR.gameObject.SetActive(right);

        if (GunsideL != null && GunsideL.gameObject.activeSelf != left)
            GunsideL.gameObject.SetActive(left);

        if (Gunup != null && Gunup.gameObject.activeSelf != up)
            Gunup.gameObject.SetActive(up);

        if (Gundown != null && Gundown.gameObject.activeSelf != down)
            Gundown.gameObject.SetActive(down);
    }

    private void CacheCamera()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindFirstObjectByType<Camera>();
        }
    }
}
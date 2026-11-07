using UnityEngine;

/// <summary>Host에서 가상 감지자 두 명을 조작하고 양쪽의 탐지 상태를 출력하는 임시 테스터다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyDetection), typeof(EnemyNetworkDetection))]
[AddComponentMenu("The Developer/Enemy/Testing/Enemy Detection Tester")]
public sealed class EnemyDetectionTester : MonoBehaviour
{
    // 실제 플레이어 감지기 연결 전까지 사용하는 테스트 전용 ID다.
    private const int DetectorA = int.MinValue;
    private const int DetectorB = int.MinValue + 1;

    [Header("임시 탐지 테스트 — 검증 후 제거 가능")]
    [SerializeField] private EnemyDetection enemyDetection;

    private EnemyNetworkDetection _networkDetection;

    private void Awake()
    {
        if (enemyDetection == null)
            enemyDetection = GetComponent<EnemyDetection>();

        _networkDetection = GetComponent<EnemyNetworkDetection>();
    }

    private void OnEnable()
    {
        enemyDetection.DetectionChanged += OnDetectionChanged;
    }

    private void Start()
    {
        PrintState();
    }

    private void OnDisable()
    {
        enemyDetection.DetectionChanged -= OnDetectionChanged;

        if (!CanChangeDetection)
            return;

        enemyDetection.UnregisterDetector(DetectorA);
        enemyDetection.UnregisterDetector(DetectorB);
    }

    [ContextMenu("감지 테스트/A 등록")]
    public void RegisterA() => ChangeDetector(DetectorA, true, "A");

    [ContextMenu("감지 테스트/B 등록")]
    public void RegisterB() => ChangeDetector(DetectorB, true, "B");

    [ContextMenu("감지 테스트/A 해제")]
    public void UnregisterA() => ChangeDetector(DetectorA, false, "A");

    [ContextMenu("감지 테스트/B 해제")]
    public void UnregisterB() => ChangeDetector(DetectorB, false, "B");

    [ContextMenu("감지 테스트/현재 상태 출력")]
    public void PrintState()
    {
        if (enemyDetection != null)
            LogState("현재 상태", enemyDetection.IsDetected);
    }

    private bool CanChangeDetection =>
        Application.isPlaying && _networkDetection != null &&
        _networkDetection.IsSpawned && _networkDetection.IsServer;

    private void ChangeDetector(int detectorId, bool register, string label)
    {
        if (!CanChangeDetection)
        {
            Debug.LogWarning("Host에서 스폰된 BlackWolf 인스턴스를 선택해 감지 테스트를 실행하세요.", this);
            return;
        }

        if (register)
            enemyDetection.RegisterDetector(detectorId);
        else
            enemyDetection.UnregisterDetector(detectorId);

        LogState(label + (register ? " 등록" : " 해제"), enemyDetection.IsDetected);
    }

    private void OnDetectionChanged(bool detected)
    {
        LogState("탐지 상태 변경", detected);
    }

    private void LogState(string action, bool detected)
    {
        string side = _networkDetection != null && _networkDetection.IsSpawned
            ? (_networkDetection.IsServer ? "Host/Server" : "Client")
            : "Offline";

        Debug.Log($"[EnemyDetectionTester][{side}][{name}] {action}: IsDetected={detected}", this);
    }
}

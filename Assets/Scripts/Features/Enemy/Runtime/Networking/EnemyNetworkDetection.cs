using Unity.Netcode;
using UnityEngine;

/// <summary>서버의 탐지 상태를 Client에 동기화한다.</summary>
[RequireComponent(typeof(NetworkObject), typeof(EnemyDetection))]
public sealed class EnemyNetworkDetection : NetworkBehaviour
{

    [Header("참조")]
    [SerializeField] private EnemyDetection enemyDetection;
    private readonly NetworkVariable<bool> _isDetected = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Unity 생명주기

    /// <summary>동기화할 로컬 탐지 상태 컴포넌트를 연결한다.</summary>
    private void Awake()
    {
        if (enemyDetection == null)
            enemyDetection = GetComponent<EnemyDetection>();
    }

    /// <summary>
    /// 서버는 탐지 변화를 발행하고 클라이언트는 동기화된 현재 상태와 이후 변화를 반영한다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            enemyDetection.DetectionChanged += OnDetectionChanged;
            PublishDetectionState(enemyDetection.IsDetected);
        }
        else
        {
            _isDetected.OnValueChanged += OnNetworkDetectionChanged;
            enemyDetection.ApplyDetectionState(_isDetected.Value);
        }
    }

    /// <summary>
    /// 탐지 이벤트와 NetworkVariable 구독을 해제한다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        enemyDetection.DetectionChanged -= OnDetectionChanged;
        _isDetected.OnValueChanged -= OnNetworkDetectionChanged;
        base.OnNetworkDespawn();
    }

    // 내부 처리

    /// <summary>서버에서 관측한 탐지 변화를 동기화 값에 전달한다.</summary>
    private void OnDetectionChanged(bool detected)
    {
        PublishDetectionState(detected);
    }

    /// <summary>서버의 탐지 상태가 바뀐 경우에만 네트워크 값을 갱신한다.</summary>
    private void PublishDetectionState(bool detected)
    {
        if (!IsSpawned || !IsServer)
            return;

        if (_isDetected.Value != detected)
            _isDetected.Value = detected;
    }

    /// <summary>클라이언트가 수신한 탐지 상태를 로컬 탐지 컴포넌트에 적용한다.</summary>
    private void OnNetworkDetectionChanged(bool previous, bool current)
    {
        enemyDetection.ApplyDetectionState(current);
    }
}

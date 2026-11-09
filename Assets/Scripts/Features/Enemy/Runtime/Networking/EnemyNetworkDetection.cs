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

    private void OnDetectionChanged(bool detected)
    {
        PublishDetectionState(detected);
    }

    private void PublishDetectionState(bool detected)
    {
        if (!IsSpawned || !IsServer)
            return;

        if (_isDetected.Value != detected)
            _isDetected.Value = detected;
    }

    private void OnNetworkDetectionChanged(bool previous, bool current)
    {
        enemyDetection.ApplyDetectionState(current);
    }
}

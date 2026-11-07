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

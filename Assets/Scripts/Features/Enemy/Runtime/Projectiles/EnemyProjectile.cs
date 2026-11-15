using TeamHJD.Game.Presentation.Scene;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>투사체의 실행 권한, 수명, 깊이 정렬과 제거를 관리한다.</summary>
[RequireComponent(typeof(NetworkObject))]
public abstract partial class EnemyProjectile : NetworkBehaviour
{
    // Inspector 설정과 제작 참조

    [Header("수명")]
    [SerializeField, Min(0.01f)] private float lifetime = 5f;

    [Header("깊이 범위")]
    [SerializeField] private float minDepth = 65f;
    [SerializeField] private float maxDepth = 70f;

    // 실행 중 참조와 상태
    private TilemapRenderer _groundRenderer;
    protected bool _isInitialized;
    protected double _expiresAt;
    protected readonly NetworkVariable<ulong> _ownerId = new(ulong.MaxValue);
    protected FieldSimulationActor _simulation;

    protected bool CanSimulate => _isInitialized && (!IsSpawned || IsServer) && FieldSimulationTime.CanAdvance(this);

    // Unity 생명주기

    /// <summary>투사체의 표현 깊이를 계산할 같은 씬의 지면을 찾는다.</summary>
    protected virtual void Awake()
    {
        _groundRenderer = FindGroundRenderer();
    }

    /// <summary>동기화된 생성자와 같은 Match의 시계·수명에 연결한다.</summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        BindReplicatedOwner();
        _simulation?.Synchronize();
    }

    /// <summary>
    /// 생성자의 수명 취소와 투사체 만료를 확인하고 실행 권한이 있으면 깊이를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        BindReplicatedOwner();
        if (!_isInitialized) return;
        if (_simulation != null && _simulation.GameplayCancellation.IsCancellationRequested)
        {
            Remove();
            return;
        }
        if (!CanSimulate) return;
        if (FieldSimulationTime.Seconds(this) >= _expiresAt)
        {
            Remove();
            return;
        }
        UpdateDepth();
    }

    /// <summary>Match 시계 연결을 해제하고 투사체 초기화 상태를 비운다.</summary>
    public override void OnNetworkDespawn()
    {
        if (_simulation?.TimeSource != null) _simulation.Unbind(_simulation.TimeSource);
        _simulation = null;
        _isInitialized = false;
        base.OnNetworkDespawn();
    }

    // 외부 호출과 상태 변경

    /// <summary>생성자의 Match 시계와 종료 수명을 공유한다.</summary>
    public virtual void BindOwner(Component owner, bool destroyOnCompletion = false)
    {
        if (owner == null) return;
        FieldSimulationTime.BindSpawned(gameObject, owner, destroyOnCompletion);
        _simulation = GetComponent<FieldSimulationActor>();
        var source = owner.GetComponentInParent<NetworkObject>();
        if (source != null && source.IsSpawned) _ownerId.Value = source.NetworkObjectId;
    }

    // 파생 클래스 공통 처리

    /// <summary>
    /// 초기화 완료를 표시하고 생성자에게 연결된 시계를 기준으로 만료 시점을 정한다.
    /// </summary>
    protected void BeginLifetime()
    {
        _isInitialized = true;
        UpdateDepth();
        _expiresAt = FieldSimulationTime.Seconds(this) + lifetime;
    }

    /// <summary>
    /// 현재 지면의 Y 범위를 기준으로 투사체의 표현용 Z를 정규화한다.
    /// </summary>
    protected void UpdateDepth()
    {
        if (_groundRenderer == null)
            return;

        Bounds groundBounds = _groundRenderer.bounds;
        Vector3 position = transform.position;
        float normalizedY = Mathf.InverseLerp(
            groundBounds.min.y, groundBounds.max.y, position.y);
        position.z = Mathf.Lerp(minDepth, maxDepth, normalizedY);
        transform.position = position;
    }

    /// <summary>
    /// 네트워크 개체는 서버에서 Despawn하고 로컬 개체는 파괴한다.
    /// </summary>
    protected void Remove()
    {
        if (IsSpawned && !IsServer)
            return;

        _isInitialized = false;

        if (IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }

    // 내부 처리

    /// <summary>
    /// 동기화된 생성자 ID로 클라이언트에서도 같은 Match 시계와 수명 연결을 복구한다.
    /// </summary>
    private void BindReplicatedOwner()
    {
        if (_simulation?.TimeSource != null || !IsSpawned || NetworkManager?.SpawnManager == null ||
            !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(_ownerId.Value, out var owner)) return;
        if (owner.TryGetComponent<EnemySpawnExecutor>(out var executor))
        {
            _simulation = executor.BindCheckpointProjectile(gameObject);
            return;
        }
        if (owner.GetComponent<FieldSimulationActor>()?.TimeSource == null) return;
        FieldSimulationTime.BindSpawned(gameObject, owner, false);
        _simulation = GetComponent<FieldSimulationActor>();
    }

    /// <summary>
    /// 같은 씬의 실제 지면 콜라이더를 가진 Tilemap 중 가장 넓은 렌더러를 찾는다.
    /// </summary>
    private TilemapRenderer FindGroundRenderer()
    {
        TilemapRenderer groundRenderer = null;
        long largestArea = 0;

        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tilemap.gameObject.scene != gameObject.scene ||
                !tilemap.TryGetComponent(out TilemapCollider2D groundCollider) ||
                !groundCollider.enabled || groundCollider.isTrigger ||
                !tilemap.TryGetComponent(out TilemapRenderer renderer))
                continue;

            long area = (long)tilemap.size.x * tilemap.size.y;
            if (area <= largestArea)
                continue;

            largestArea = area;
            groundRenderer = renderer;
        }

        return groundRenderer;
    }
}

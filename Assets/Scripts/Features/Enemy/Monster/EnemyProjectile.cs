using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>투사체의 실행 권한, 수명, 깊이 정렬과 제거를 관리한다.</summary>
[RequireComponent(typeof(NetworkObject))]
public abstract class EnemyProjectile : NetworkBehaviour
{
    [Header("수명")]
    [SerializeField, Min(0.01f)] private float lifetime = 5f;

    [Header("깊이 범위")]
    [SerializeField] private float minDepth = 65f;
    [SerializeField] private float maxDepth = 70f;

    private TilemapRenderer _groundRenderer;
    private bool _isInitialized;

    protected bool CanSimulate => _isInitialized && (!IsSpawned || IsServer);

    protected virtual void Awake()
    {
        _groundRenderer = FindGroundRenderer();
    }

    private void LateUpdate()
    {
        if (CanSimulate)
            UpdateDepth();
    }

    protected void BeginLifetime()
    {
        _isInitialized = true;
        UpdateDepth();
        CancelInvoke(nameof(Remove));
        Invoke(nameof(Remove), lifetime);
    }

    public override void OnNetworkDespawn()
    {
        CancelInvoke(nameof(Remove));
        _isInitialized = false;
        base.OnNetworkDespawn();
    }

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

    protected void Remove()
    {
        if (IsSpawned && !IsServer)
            return;

        CancelInvoke(nameof(Remove));
        _isInitialized = false;

        if (IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }
}

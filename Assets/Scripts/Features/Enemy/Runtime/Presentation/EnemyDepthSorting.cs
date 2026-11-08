using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 현재 씬의 지면 높이에 맞춰 적의 렌더링 깊이를 조정한다.
/// </summary>
public sealed class EnemyDepthSorting : MonoBehaviour
{
    [Header("깊이 범위")]
    [SerializeField] private float minDepth = 65f;
    [SerializeField] private float maxDepth = 70f;

    private TilemapRenderer _groundRenderer;
    private NetworkObject _networkObject;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();
        _groundRenderer = FindGroundRenderer();
    }

    /// <summary>
    /// 서버 또는 로컬 실행에서 지면의 Y 범위를 표현용 Z 범위로 변환한다.
    /// </summary>
    private void LateUpdate()
    {
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.NetworkManager.IsServer)
            return;

        if (_groundRenderer == null)
            _groundRenderer = FindGroundRenderer();

        if (_groundRenderer == null)
            return;

        Bounds groundBounds = _groundRenderer.bounds;
        float normalizedY = Mathf.InverseLerp(groundBounds.min.y, groundBounds.max.y, transform.position.y);
        Vector3 position = transform.position;
        position.z = Mathf.Lerp(minDepth, maxDepth, normalizedY);
        transform.position = position;
    }

    /// <summary>
    /// 같은 씬의 활성 비트리거 TilemapCollider를 가진 지면 중 가장 넓은 Tilemap을 찾는다.
    /// </summary>
    private TilemapRenderer FindGroundRenderer()
    {
        TilemapRenderer groundRenderer = null;
        long largestArea = 0;

        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tilemap.gameObject.scene != gameObject.scene ||
                !tilemap.TryGetComponent(out TilemapCollider2D collider) ||
                !collider.enabled || collider.isTrigger ||
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

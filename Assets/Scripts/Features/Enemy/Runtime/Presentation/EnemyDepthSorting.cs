using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 발 콜라이더의 중심 높이를 기준으로 적의 렌더링 깊이를 조정한다.
/// </summary>
public sealed class EnemyDepthSorting : MonoBehaviour
{
    [Header("정렬 기준")]
    [SerializeField] private BoxCollider2D footCollider;

    [Header("깊이 범위")]
    [SerializeField] private float minDepth = 65f;
    [SerializeField] private float maxDepth = 70f;

    private TilemapRenderer _groundRenderer;
    private NetworkObject _networkObject;

    private void Awake()
    {
        if (footCollider == null)
            footCollider = GetComponent<BoxCollider2D>();

        _networkObject = GetComponent<NetworkObject>();
        _groundRenderer = FindGroundRenderer();
    }

    /// <summary>
    /// 서버 또는 로컬 실행에서 발 중심의 Y를 지면 범위에 맞춰 표현용 Z로 변환한다.
    /// </summary>
    private void LateUpdate()
    {
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.NetworkManager.IsServer)
            return;

        if (footCollider == null || !footCollider.enabled || !footCollider.gameObject.activeInHierarchy)
            return;

        if (_groundRenderer == null)
            _groundRenderer = FindGroundRenderer();

        if (_groundRenderer == null)
            return;

        Bounds groundBounds = _groundRenderer.bounds;
        // 몸통 높이 대신 Offset과 월드 스케일이 반영된 발 중심으로 앞뒤를 비교한다.
        float sortY = footCollider.bounds.center.y;
        float normalizedY = Mathf.InverseLerp(groundBounds.min.y, groundBounds.max.y, sortY);
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

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>임시포탑 바닥의 지면 포함 여부와 캐릭터·장애물·포탑 겹침을 검사합니다.</summary>
public static class PlayerTurretPlacementRules
{
    private const float Padding = .05f;

    public static Tilemap FindGround(Scene scene)
    {
        Tilemap ground = null;
        long largestArea = 0;
        foreach (Tilemap map in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (map.gameObject.scene != scene || !map.TryGetComponent(out TilemapCollider2D collider) ||
                !collider.enabled || collider.isTrigger) continue;

            long area = (long)map.size.x * map.size.y;
            if (area <= largestArea) continue;
            ground = map;
            largestArea = area;
        }
        return ground;
    }

    public static bool CanPlace(GameObject prefab, Vector3 position, GameObject player, Tilemap ground) =>
        Evaluate(prefab, position, player, ground) == PlayerTurretPlacementResult.Allowed;

    public static PlayerTurretPlacementResult Evaluate(GameObject prefab, Vector3 position, GameObject player, Tilemap ground)
    {
        if (player == null || ground == null || !ground.isActiveAndEnabled ||
            ground.gameObject.scene != player.scene) return PlayerTurretPlacementResult.GroundUnavailable;
        if (!TryGetFootprint(prefab, position, out Bounds footprint)) return PlayerTurretPlacementResult.InvalidFootprint;

        footprint.Expand(new Vector3(Padding * 2f, Padding * 2f, 0f));
        if (!IsOnGround(ground, footprint)) return PlayerTurretPlacementResult.OutsideGround;

        foreach (Collider2D hit in Physics2D.OverlapBoxAll(footprint.center, footprint.size, 0f))
        {
            if (hit == null || hit.isTrigger || hit.gameObject.scene != player.scene) continue;
            int layer = hit.gameObject.layer;
            if (IsBulletLayer(layer)) continue;

            // 캐릭터가 점유한 공간은 물리 충돌 행렬과 관계없이 설치를 막습니다.
            if (hit.transform.IsChildOf(player.transform) || hit.GetComponentInParent<PlayerInfo>() != null ||
                layer == LayerMask.NameToLayer("Player")) return PlayerTurretPlacementResult.OverlapsPlayer;
            if (hit.GetComponentInParent<EnemyHealth>() != null || layer == LayerMask.NameToLayer("Enemy"))
                return PlayerTurretPlacementResult.OverlapsEnemy;

            // 사거리 트리거는 제외하지만 실제 포탑 본체는 충돌 행렬과 관계없이 설치를 막습니다.
            if (hit.GetComponentInParent<PlayerTurret>() != null ||
                hit.GetComponentInParent<TeamHJD.Game.FieldTurrets.FieldTurretBase>() != null)
                return PlayerTurretPlacementResult.OverlapsTurret;
            if ((hit.excludeLayers.value & (1 << player.layer)) != 0 ||
                Physics2D.GetIgnoreLayerCollision(player.layer, layer)) continue;
            return PlayerTurretPlacementResult.BlockedByObstacle;
        }
        return PlayerTurretPlacementResult.Allowed;
    }

    public static bool TryGetFootprint(GameObject prefab, Vector3 position, out Bounds footprint)
    {
        footprint = default;
        if (prefab == null || !IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z) ||
            !prefab.TryGetComponent(out SpriteRenderer renderer) || renderer.sprite == null) return false;

        // 현재 프리팹의 루트 SpriteRenderer는 고정 바닥입니다. 회전하는 포신은 포함하지 않습니다.
        Bounds local = renderer.localBounds;
        // 바닥 외형과 실제 통과 차단 영역 중 더 넓은 쪽까지 설치 공간으로 확보합니다.
        if (prefab.TryGetComponent(out BoxCollider2D body) && body.enabled && !body.isTrigger)
            local.Encapsulate(new Bounds(body.offset, body.size));
        Vector3 scale = prefab.transform.localScale;
        Vector3 size = Vector3.Scale(local.size, scale);
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0f);
        Vector3 center = position + Vector3.Scale(local.center, scale);
        if (!IsFinite(size.x) || !IsFinite(size.y) || size.x <= 0f || size.y <= 0f) return false;
        footprint = new Bounds(center, size);
        return true;
    }

    private static bool IsOnGround(Tilemap ground, Bounds footprint)
    {
        // 정렬용 플레이어 Z가 Tilemap 셀 Z에 섞이지 않도록 지면 평면에서 조회합니다.
        float z = ground.transform.position.z;
        Vector3Int min = ground.WorldToCell(new Vector3(footprint.min.x, footprint.min.y, z));
        Vector3Int max = ground.WorldToCell(new Vector3(footprint.max.x, footprint.max.y, z));
        if (!ground.cellBounds.Contains(min) || !ground.cellBounds.Contains(max)) return false;
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
            if (!ground.HasTile(new Vector3Int(x, y, min.z))) return false;
        return true;
    }

    private static bool IsBulletLayer(int layer) => layer == LayerMask.NameToLayer("Bullet")
        || layer == LayerMask.NameToLayer("EmenyBullet") || layer == LayerMask.NameToLayer("PlayerBulletCollider");

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

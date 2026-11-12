using UnityEngine;

public enum PlayerBombPlacementResult { Allowed, InvalidBody, OverlapsEnemy, BlockedByObstacle }

/// <summary>폭탄 본체 후보 위치의 점유 공간을 검사합니다. 설치자 겹침·지면 검사는 공통 설치 흐름에서 보완합니다.</summary>
public static class PlayerBombPlacementRules
{
    private const float Clearance = .02f;

    public static PlayerBombPlacementResult Evaluate(GameObject prefab, Vector3 position, GameObject owner)
    {
        if (owner == null || !TryGetFootprint(prefab, position, out Bounds footprint))
            return PlayerBombPlacementResult.InvalidBody;

        footprint.Expand(new Vector3(Clearance * 2, Clearance * 2, 0));
        Physics2D.SyncTransforms();
        foreach (var hit in Physics2D.OverlapBoxAll(footprint.center, footprint.size, 0f))
        {
            if (hit == null || hit.isTrigger || hit.gameObject.scene != owner.scene ||
                hit.transform.IsChildOf(owner.transform)) continue;
            int layer = hit.gameObject.layer;
            if (IsBulletLayer(layer)) continue;
            // 몬스터는 플레이어와의 충돌 행렬에 관계없이 설치 공간을 점유합니다.
            if (layer == LayerMask.NameToLayer("Enemy") || hit.GetComponentInParent<EnemyHealth>() != null)
                return PlayerBombPlacementResult.OverlapsEnemy;
            if (layer == LayerMask.NameToLayer("PlayerFeetBox") || hit.GetComponentInParent<PlayerInfo>() != null ||
                hit.GetComponentInParent<PlayerTurret>() != null ||
                hit.GetComponentInParent<TeamHJD.Game.FieldTurrets.FieldTurretBase>() != null)
                return PlayerBombPlacementResult.BlockedByObstacle;

            int ownerLayer = 1 << owner.layer;
            if ((hit.excludeLayers.value & ownerLayer) != 0) continue;
            // 본체의 명시적 Include Layers는 전역 레이어 무시 설정보다 우선합니다.
            if ((hit.includeLayers.value & ownerLayer) == 0 && Physics2D.GetIgnoreLayerCollision(owner.layer, layer)) continue;
            return PlayerBombPlacementResult.BlockedByObstacle;
        }
        return PlayerBombPlacementResult.Allowed;
    }

    public static bool TryGetFootprint(GameObject prefab, Vector3 position, out Bounds footprint)
    {
        footprint = default;
        if (prefab == null || !IsFinite(position) ||
            !prefab.TryGetComponent(out PolygonCollider2D body) || !body.enabled || body.isTrigger) return false;

        // Prefab 자산의 bounds는 비어 있을 수 있으므로 편집된 점·오프셋·스케일로 계산합니다.
        Matrix4x4 placement = Matrix4x4.TRS(position, Quaternion.identity, prefab.transform.localScale);
        bool hasPoint = false;
        for (int path = 0; path < body.pathCount; path++)
        foreach (var point in body.GetPath(path))
        {
            Vector3 world = placement.MultiplyPoint3x4(point + body.offset);
            if (!IsFinite(world)) return false;
            if (!hasPoint) { footprint = new Bounds(world, Vector3.zero); hasPoint = true; }
            else footprint.Encapsulate(world);
        }
        return hasPoint && footprint.size.x > 0 && footprint.size.y > 0;
    }

    private static bool IsBulletLayer(int layer) => layer == LayerMask.NameToLayer("Bullet")
        || layer == LayerMask.NameToLayer("EnemyProjectile") || layer == LayerMask.NameToLayer("PlayerHitBox");

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
}

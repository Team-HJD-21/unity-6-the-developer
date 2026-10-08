using UnityEngine;

/// <summary>방벽 설치 구간의 지형 충돌을 판정합니다.</summary>
public static class PlayerBarrierPlacementRules
{
    public static bool IsClear(GameObject player, LayerMask terrainLayers, float width, Vector3 start, Vector3 end)
    {
        Vector2 delta = end - start;
        var size = new Vector2(delta.magnitude + width, width + .2f);
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        foreach (var hit in Physics2D.OverlapBoxAll((start + end) * .5f, size, angle, terrainLayers))
        {
            // Legacy 제거로 인해 로직 무효화: 구형 Monster 설치 충돌 제외 검사.
            // hit.GetComponentInParent<Monster>() != null
            if (hit == null || hit.isTrigger || hit.GetComponentInParent<PlayerInputHandler>() != null ||
                hit.GetComponentInParent<EnemyHealth>() != null) continue;
            int layer = hit.gameObject.layer;
            if ((hit.excludeLayers.value & (1 << player.layer)) != 0) continue;
            if (layer == LayerMask.NameToLayer("Bullet") || layer == LayerMask.NameToLayer("PlayerBulletCollider") ||
                layer == LayerMask.NameToLayer("EmenyBullet")) continue;
            if (Physics2D.GetIgnoreLayerCollision(player.layer, layer)) continue;
            return false;
        }
        return true;
    }
}

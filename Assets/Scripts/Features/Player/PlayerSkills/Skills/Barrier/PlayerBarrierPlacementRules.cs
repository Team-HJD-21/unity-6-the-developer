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
            if (hit == null || hit.isTrigger || hit.gameObject.scene != player.scene) continue;
            int layer = hit.gameObject.layer;
            if (layer == LayerMask.NameToLayer("Bullet") || layer == LayerMask.NameToLayer("PlayerHitBox") ||
                layer == LayerMask.NameToLayer("EnemyProjectile")) continue;
            if (layer == LayerMask.NameToLayer("PlayerFeetBox") || layer == LayerMask.NameToLayer("Enemy") ||
                hit.GetComponentInParent<PlayerInputHandler>() != null || hit.GetComponentInParent<EnemyHealth>() != null ||
                hit.GetComponentInParent<PlayerTurret>() != null ||
                hit.GetComponentInParent<TeamHJD.Game.FieldTurrets.FieldTurretBase>() != null) return false;
            int mask = 1 << player.layer;
            if ((hit.excludeLayers.value & mask) != 0) continue;
            if ((hit.includeLayers.value & mask) == 0 && Physics2D.GetIgnoreLayerCollision(player.layer, layer)) continue;
            return false;
        }
        return true;
    }
}

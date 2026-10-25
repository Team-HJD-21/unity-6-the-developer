using UnityEngine;

/// <summary>스패너 공격과 수리가 공유하는 거리·전방·장애물 검사를 담당합니다.</summary>
public static class PlayerSpannerTargeting
{
    public static bool CanReach(Transform owner, Transform target, Vector2 point,
        Vector2 direction, float range, float arc)
    {
        if (owner == null || target == null || owner.gameObject.scene != target.gameObject.scene ||
            !PlayerAimUtility.IsValidDirection(direction) || float.IsNaN(range) || float.IsInfinity(range) ||
            range <= 0f || float.IsNaN(arc) || float.IsInfinity(arc)) return false;
        Vector2 delta = point - (Vector2)owner.position;
        if (float.IsNaN(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.x) || float.IsInfinity(delta.y) ||
            delta.sqrMagnitude > range * range) return false;
        if (delta.sqrMagnitude <= .0001f) return true;
        if (Vector2.Angle(direction, delta) > Mathf.Clamp(arc, 1f, 180f) * .5f) return false;

        foreach (RaycastHit2D hit in Physics2D.RaycastAll(owner.position, delta.normalized, delta.magnitude))
        {
            Collider2D obstacle = hit.collider;
            if (obstacle == null || obstacle.isTrigger || obstacle.gameObject.scene != owner.gameObject.scene ||
                obstacle.transform.IsChildOf(owner) || obstacle.transform.IsChildOf(target) ||
                obstacle.GetComponentInParent<EnemyHealth>() != null) continue;
            int ownerLayer = owner.gameObject.layer;
            if (Physics2D.GetIgnoreLayerCollision(ownerLayer, obstacle.gameObject.layer) ||
                (obstacle.excludeLayers.value & (1 << ownerLayer)) != 0) continue;
            return false;
        }
        return true;
    }
}

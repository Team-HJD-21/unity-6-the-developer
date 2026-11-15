using TeamHJD.Game.Contracts;
using UnityEngine;

/// <summary>스패너 공격과 수리가 공유하는 거리·전방·장애물 검사를 담당합니다.</summary>
public static class PlayerSpannerTargeting
{
    // Player용 Clock과 보류 가능한 FieldClock은 객체가 달라도 같은 실행에 속합니다.
    // 판 ID뿐 아니라 복원/재시작 세대도 같아야 이전 판의 터렛을 수리하지 않습니다.
    internal static bool SharesMatchExecution(IMatchTime owner, IMatchTime target)
    {
        if (ReferenceEquals(owner, target)) return owner == null || owner.IsAvailable;
        if (owner == null || target == null || !owner.IsAvailable || !target.IsAvailable) return false;
        var player = owner.Snapshot;
        var turret = target.Snapshot;
        return player != null && turret != null && player.IsAvailable && turret.IsAvailable &&
            player.MatchId == turret.MatchId && player.Epoch == turret.Epoch;
    }

    public static bool CanReach(Transform owner, Transform target, Vector2 point,
        Vector2 direction, float range, float arc, Vector2? interactionOrigin = null)
    {
        if (owner == null || target == null || owner.gameObject.scene != target.gameObject.scene ||
            !PlayerAimUtility.IsValidDirection(direction) || float.IsNaN(range) || float.IsInfinity(range) ||
            range <= 0f || float.IsNaN(arc) || float.IsInfinity(arc)) return false;
        Vector2 origin = interactionOrigin ?? (Vector2)owner.position;
        Vector2 delta = point - origin;
        if (float.IsNaN(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.x) || float.IsInfinity(delta.y) ||
            delta.sqrMagnitude > range * range) return false;
        if (delta.sqrMagnitude <= .0001f) return true;
        if (Vector2.Angle(direction, delta) > Mathf.Clamp(arc, 1f, 180f) * .5f) return false;

        foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, delta.normalized, delta.magnitude))
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

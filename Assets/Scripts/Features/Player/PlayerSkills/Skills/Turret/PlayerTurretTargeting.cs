using System.Collections.Generic;
using TeamHJD.Game.Enemy.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>플레이어 터렛 사거리 안의 살아 있는 적을 검색합니다.</summary>
public static class PlayerTurretTargeting
{
    public static Transform FindClosest(Vector3 origin, float attackRange, string enemyTag, Component owner = null, List<Collider2D> results = null)
    {
        if (!float.IsFinite(attackRange) || attackRange <= 0 || (owner != null && !FieldSimulationTime.CanAdvance(owner))) return null;
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(LayerMask.GetMask("EnemyHurtbox"));
        results ??= new List<Collider2D>(32);
        var physics = owner != null ? owner.gameObject.scene.GetPhysicsScene2D() : Physics2D.defaultPhysicsScene;
        physics.OverlapCircle(origin, attackRange, filter, results);
        float closestDistanceSqr = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (var hit in results)
        {
            if (hit == null) continue;
            var health = hit.GetComponentInParent<EnemyHealth>();
            if (!IsCurrentTarget(health, owner) || (!string.IsNullOrEmpty(enemyTag) && !health.CompareTag(enemyTag))) continue;
            float distSqr = ((Vector2)hit.ClosestPoint(origin) - (Vector2)origin).sqrMagnitude;
            if (distSqr >= closestDistanceSqr) continue;
            closestDistanceSqr = distSqr;
            closestEnemy = hit.transform;
        }

        return closestEnemy;
    }

    public static Vector3 AimPoint(Transform target)
    {
        var collider = target != null ? target.GetComponent<Collider2D>() : null;
        return collider != null ? collider.bounds.center : target != null ? target.position : Vector3.zero;
    }

    internal static bool IsCurrentTarget(EnemyHealth health, Component owner)
    {
        if (health == null || !health.CanBeAttackedBy(AttackSourceType.Turret) || !FieldSimulationTime.CanAdvance(health)) return false;
        if (owner == null) return true;
        if (health.gameObject.scene != owner.gameObject.scene) return false;
        var source = owner.GetComponentInParent<PlayerMatchPresence>()?.TimeSource ?? owner.GetComponentInParent<FieldSimulationActor>()?.TimeSource;
        var target = health.GetComponentInParent<FieldSimulationActor>()?.TimeSource;
        if (source == null || target == null) return source == null && target == null;
        if (!source.IsAvailable || !target.IsAvailable) return false;
        return source.Snapshot.MatchId == target.Snapshot.MatchId && source.Snapshot.Epoch == target.Snapshot.Epoch;
    }
}

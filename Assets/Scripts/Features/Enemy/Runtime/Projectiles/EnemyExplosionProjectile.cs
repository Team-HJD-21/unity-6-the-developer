using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 직격 지점 주변의 대상에게 중복 없이 폭발 피해를 준다.
/// </summary>
public class EnemyExplosionProjectile : EnemyProjectileWithImpactEffect
{
    [Header("폭발 설정")]
    [SerializeField, Min(0.01f)] private float explosionRadius = 1f;
    

    private readonly List<Collider2D> _overlapResults = new(32);
    private readonly HashSet<ITargetable> _damagedTargets = new();
    
    /// <summary>
    /// 직격 위치를 중심으로 직격 대상과 주변 대상에게 폭발 피해를 준다.
    /// </summary>
    protected override void OnHit(ITargetable target)
    {
        ApplyExplosionDamage(target, transform.position);
    }
    
    /// <summary>
    /// 직격 대상을 포함한 주변 후보에 피해를 주고 여러 콜라이더로 인한 중복 적용을 막는다.
    /// </summary>
    private void ApplyExplosionDamage(
        ITargetable directTarget,
        Vector2 position)
    {
        _overlapResults.Clear();
        _damagedTargets.Clear();

        // 직격 대상도 범위 피해에 포함하며, 중복 피해는 막는다.
        _damagedTargets.Add(directTarget);
        ApplyDamage(directTarget);

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useDepth = false,
            useTriggers = true
        };

        Physics2D.OverlapCircle(
            position,
            explosionRadius,
            filter,
            _overlapResults);

        foreach (Collider2D hit in _overlapResults)
        {
            if (hit == null)
                continue;

            ITargetable target =
                hit.GetComponentInParent<ITargetable>();

            if (target is not UnityEngine.Object targetObject ||
                targetObject == null ||
                !target.CanBeTargeted)
            {
                continue;
            }

            if (!_damagedTargets.Add(target))
                continue;

            ApplyDamage(target);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}

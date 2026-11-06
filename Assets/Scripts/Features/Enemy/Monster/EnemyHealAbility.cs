using System.Collections.Generic;
using UnityEngine;

public class EnemyHealAbility : EnemyAbility 
{
    [Header("참조")]
    [SerializeField] private HealingOrbProjectile projectilePrefab;
    [SerializeField] private EnemyProjectileSpawner projectileSpawner;
    
    [Header("회복 설정")]
    [SerializeField, Min(1)] private int healAmount = 10;
    
    private EnemyHealth _selfHealth;
    private EnemyHealth _healTarget;
    private readonly List<Collider2D> _overlapResults = new(32);

    public override Transform FacingTarget =>
        _healTarget != null ? _healTarget.transform : null;
    
    protected override void Awake()
    {
        base.Awake();
        
        _selfHealth = GetComponent<EnemyHealth>();
        
        if (projectileSpawner == null)
            projectileSpawner = GetComponent<EnemyProjectileSpawner>();
    }

    public override bool CheckCondition()
    {
        return projectilePrefab != null &&
               projectileSpawner != null &&
               healAmount > 0 &&
               TryFindHealTarget(out _healTarget);
    }

    protected override bool Activate()
    {
        if (_healTarget == null ||
            _healTarget == _selfHealth ||
            !_healTarget.CanBeHealed)
        {
            return false;
        }

        return projectileSpawner.TrySpawn(
            projectilePrefab,
            transform.position,
            _healTarget,
            healAmount);
    }

    private bool TryFindHealTarget(out EnemyHealth target)
    {
        target = null;

        if (_bodyCollider == null || _selfHealth == null)
            return false;
        
        _overlapResults.Clear();
        
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useDepth = false,
            useTriggers = true
        };
        
        Bounds bounds = _bodyCollider.bounds;
        
        float searchRadius = bounds.extents.magnitude + activationRange;

        Physics2D.OverlapCircle(
            bounds.center,
            searchRadius,
            filter,
            _overlapResults);

        float closestDistance = float.PositiveInfinity;
        
        foreach (Collider2D hit in _overlapResults)
        {
            EnemyHealth health = hit.GetComponentInParent<EnemyHealth>();
            
            if(health == null || health == _selfHealth)
                continue;
            
            if(!health.CanBeHealed)
                continue;

            Collider2D[] colliders = health.GetComponentsInChildren<Collider2D>();

            if (!IsInRange(colliders, activationRange))
                continue;
            
            float distance =
                ((Vector2)health.transform.position -
                 (Vector2)transform.position).sqrMagnitude;
            
            if (distance >= closestDistance)
                continue;
            
            closestDistance = distance;
            target = health;
        }

        return target != null;
    }
}

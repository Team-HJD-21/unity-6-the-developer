using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자신을 제외한 회복 가능한 아군을 찾아 회복 구슬 생성을 요청한다.
/// </summary>
public class EnemyHealAbility : EnemyAbility 
{
    [Header("참조")]
    [SerializeField] private HealingOrbProjectile projectilePrefab;
    [SerializeField] private EnemyProjectileGenerator projectileSpawner;
    
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
            projectileSpawner = GetComponent<EnemyProjectileGenerator>();
    }

    /// <summary>
    /// 회복 구슬을 생성할 수 있는지 확인하고 이번에 치료할 아군을 선택한다.
    /// </summary>
    public override bool CheckCondition()
    {
        return projectilePrefab != null &&
               projectileSpawner != null &&
               healAmount > 0 &&
               TryFindHealTarget(out _healTarget);
    }

    /// <summary>
    /// 선택한 아군이 여전히 회복 가능하면 회복 구슬 생성을 요청한다.
    /// </summary>
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

    /// <summary>
    /// 주변 후보 중 자신을 제외하고 실제 능력 범위에 있는 가장 가까운 회복 대상을 찾는다.
    /// </summary>
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
        
        // 원형 조회는 후보만 모으고, 실제 발동 범위는 아래 콜라이더 간격으로 다시 확인한다.
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

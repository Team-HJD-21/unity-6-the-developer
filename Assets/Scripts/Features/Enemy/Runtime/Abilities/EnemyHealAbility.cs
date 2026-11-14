using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
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

    [Header("아군 검색")]
    [SerializeField] private LayerMask allyHitboxLayers;
    [SerializeField, Min(0f)] private float targetSearchInterval = 0.2f;

    private Collider2D[] _healTargetColliders;
    private double _nextSearchTime;
    private readonly HashSet<EnemyHealth> _checkedTargets = new();
    
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
    /// 유효한 치료 대상은 유지하고, 필요할 때만 새 대상을 검색한다.
    /// </summary>
    public override bool CheckCondition()
    {
        if (projectilePrefab == null ||
            projectileSpawner == null ||
            healAmount <= 0 ||
            _bodyCollider == null ||
            _selfHealth == null ||
            allyHitboxLayers.value == 0)
        {
            ClearHealTarget();
            return false;
        }

        if (_healTarget != null &&
            _healTarget != _selfHealth &&
            _healTarget.CanBeHealed &&
            IsInRange(_healTargetColliders, activationRange))
        {
            return true;
        }

        // 기존 대상을 잃었을 때는 검색 간격을 기다리지 않고 한 번 재선택한다.
        if (_healTargetColliders != null)
        {
            ClearHealTarget();
            _nextSearchTime = 0;
        }

        double now = FieldSimulationTime.Seconds(this);

        if (now < _nextSearchTime)
            return false;

        _nextSearchTime = now + targetSearchInterval;

        return TryFindHealTarget(
            out _healTarget,
            out _healTargetColliders);
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
    /// 피격 레이어에서 회복 가능한 아군을 찾고 가장 가까운 대상을 선택한다.
    /// 선택한 대상의 콜라이더 목록은 다음 검사에서도 재사용한다.
    /// </summary>
    private bool TryFindHealTarget(
        out EnemyHealth target,
        out Collider2D[] targetColliders)
    {
        target = null;
        targetColliders = null;

        _overlapResults.Clear();
        _checkedTargets.Clear();
        
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = allyHitboxLayers,
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

        Vector2 selfPosition = transform.position;
        float closestDistance = float.PositiveInfinity;
        
        foreach (Collider2D hit in _overlapResults)
        {
            if (hit == null)
                continue;

            EnemyHealth health = hit.GetComponentInParent<EnemyHealth>();

            if (health == null ||
                health == _selfHealth ||
                !_checkedTargets.Add(health) ||
                !health.CanBeHealed)
            {
                continue;
            }

            float distance =
                ((Vector2)health.transform.position - selfPosition).sqrMagnitude;

            if (distance >= closestDistance)
                continue;

            Collider2D[] colliders =
                health.GetComponentsInChildren<Collider2D>();

            if (!IsInRange(colliders, activationRange))
                continue;
            
            closestDistance = distance;
            target = health;
            targetColliders = colliders;
        }

        return target != null;
    }

    /// <summary>기존 치료 대상과 거리 검사 참조를 비운다.</summary>
    private void ClearHealTarget()
    {
        _healTarget = null;
        _healTargetColliders = null;
    }

    private void OnDisable()
    {
        ClearHealTarget();

        _nextSearchTime = 0;
        _overlapResults.Clear();
        _checkedTargets.Clear();
    }
}

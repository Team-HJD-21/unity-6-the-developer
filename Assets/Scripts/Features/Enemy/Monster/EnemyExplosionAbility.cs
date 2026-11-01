using System;
using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

/// <summary>
/// 타깃이 발동 거리 안에 들어오면 범위 피해를 적용하고,
/// 폭발 애니메이션 종료 후 자신의 제거를 요청한다.
/// </summary>
public sealed class EnemyExplosionAbility : EnemyAbility
{
    [Header("참조")]
    [SerializeField] private EnemyTargeting enemyTargeting;

    [Header("발동 설정")]
    [SerializeField, Min(0f)] private float activationRange = 0.3f;

    [Header("폭발 설정")]
    [SerializeField, Min(1)] private int explosionDamage = 20;
    [SerializeField, Min(0.01f)] private float explosionRadius = 1f;

    private readonly List<Collider2D> _overlapResults = new(32);
    private readonly HashSet<ITargetable> _damagedTargets = new();

    private ContactFilter2D _contactFilter;
    private EnemyHealth _enemyHealth;

    private bool _hasExploded;
    private bool _completionReported;

    public override bool IsExecuting => _hasExploded;

    /// <summary>
    /// 폭발 애니메이션 종료 후 자신의 제거를 요청한다.
    /// </summary>
    public event Action RemovalRequested;

    protected override void Awake()
    {
        if (_bodyCollider == null)
            _bodyCollider = GetComponent<CapsuleCollider2D>();

        base.Awake();

        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();

        _enemyHealth = GetComponent<EnemyHealth>();

        _contactFilter = new ContactFilter2D
        {
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false,
            useTriggers = true
        };
    }

    public override bool CheckCondition()
    {
        if (_hasExploded ||
            enemyTargeting == null ||
            enemyTargeting.Target == null)
        {
            return false;
        }

        ITargetable target =
            enemyTargeting.Target.GetComponent<ITargetable>();

        if (!IsValidTarget(target))
            return false;

        return IsInRange(
            enemyTargeting.TargetColliders,
            activationRange);
    }

    protected override bool Activate()
    {
        if (_hasExploded)
            return false;

        _hasExploded = true;

        // 폭발한 뒤 추가 피격으로 애니메이션이 중단되지 않도록 한다.
        if (_enemyHealth != null)
            _enemyHealth.Deactivate();

        ApplyExplosionDamage();

        // 기존 컨트롤러가 발동 성공 후 Attack 애니메이션을 재생한다.
        return true;
    }

    private void ApplyExplosionDamage()
    {
        _overlapResults.Clear();
        _damagedTargets.Clear();

        Physics2D.OverlapCircle(
            GetExplosionCenter(),
            explosionRadius,
            _contactFilter,
            _overlapResults);

        foreach (Collider2D hit in _overlapResults)
        {
            if (hit == null)
                continue;

            ITargetable target =
                hit.GetComponentInParent<ITargetable>();

            if (!IsValidTarget(target))
                continue;

            // 여러 콜라이더가 같은 대상을 가리켜도 피해는 한 번만 적용한다.
            if (!_damagedTargets.Add(target))
                continue;

            ApplyDamage(target);
        }
    }

    private static bool IsValidTarget(ITargetable target)
    {
        // 인터페이스로 참조한 Unity 오브젝트의 파괴 여부도 확인한다.
        if (target is not UnityEngine.Object targetObject ||
            targetObject == null ||
            !target.CanBeTargeted)
        {
            return false;
        }

        return target.TargetType == TargetType.Player ||
               target.TargetType == TargetType.Turret;
    }

    private void ApplyDamage(ITargetable target)
    {
        Transform targetTransform = target.TargetTransform;

        if (targetTransform == null)
            return;

        if (target.TargetType == TargetType.Player)
        {
            PlayerInfo player =
                targetTransform.GetComponentInParent<PlayerInfo>();

            if (player != null)
                player.TakeDamage(explosionDamage);

            return;
        }

        IDamageableTarget damageable =
            targetTransform.GetComponentInParent<IDamageableTarget>();

        if (damageable != null)
            damageable.ApplyDamage(explosionDamage);
    }

    /// <summary>
    /// Bomber 폭발 클립 마지막의 Animation Event에서 호출한다.
    /// </summary>
    public void OnExplosionAnimationFinished()
    {
        if (!_hasExploded || _completionReported)
            return;

        _completionReported = true;
        RemovalRequested?.Invoke();
    }

    private Vector3 GetExplosionCenter()
    {
        if (_bodyCollider != null)
            return _bodyCollider.bounds.center;

        // Awake 전의 Scene 뷰에서도 캡슐 기준의 폭발 중심을 표시한다.
        CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
        if (capsule != null)
            return capsule.bounds.center;

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetExplosionCenter(), explosionRadius);
    }
}

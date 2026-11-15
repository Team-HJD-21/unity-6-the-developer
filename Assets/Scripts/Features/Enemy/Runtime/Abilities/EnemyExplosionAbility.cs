// 폭발 조건·범위 피해·애니메이션 종료 제거를 관리하고 공통 피해 계약을 사용합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

/// <summary>
/// 타깃이 발동 거리 안에 들어오면 범위 피해를 적용하고,
/// 폭발 애니메이션 종료 후 자신의 제거를 요청한다.
/// </summary>
public sealed partial class EnemyExplosionAbility : EnemyAbility
{
    [Header("참조")]
    [SerializeField] private EnemyTargeting enemyTargeting;

    [Header("폭발 설정")]
    [SerializeField, Min(1)] private int explosionDamage = 20;
    [SerializeField, Min(0.01f)] private float explosionRadius = 1f;

    private readonly List<Collider2D> _overlapResults = new(32);
    private readonly HashSet<ITargetable> _damagedTargets = new();

    private ContactFilter2D _contactFilter;
    private EnemyHealth _enemyHealth;

    private bool _hasExploded;
    private bool _completionReported;
    private bool _removalDeferred;

    public override bool IsExecuting => _hasExploded;

    /// <summary>
    /// 폭발 애니메이션 종료 후 자신의 제거를 요청한다.
    /// </summary>
    public event Action RemovalRequested;

    protected override void Awake()
    {
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

    /// <summary>
    /// 아직 폭발하지 않았고 유효한 Player·Turret이 몸통 기준 발동 범위 안에 있는지 확인한다.
    /// </summary>
    public override bool CheckCondition()
    {
        return enemyTargeting != null &&
               CheckCondition(IsInRange(enemyTargeting.TargetColliders));
    }

    /// <summary>이번 행동에서 계산한 거리 결과로 폭발 조건을 확인한다.</summary>
    public override bool CheckCondition(bool isInTargetRange)
    {
        if (!isInTargetRange ||
            _hasExploded ||
            enemyTargeting == null ||
            enemyTargeting.Target == null)
        {
            return false;
        }

        ITargetable target =
            enemyTargeting.Target.GetComponent<ITargetable>();

        return IsValidTarget(target);
    }

    /// <summary>
    /// 폭발 상태를 고정하고 추가 피격을 차단한 뒤 범위 피해를 적용한다.
    /// </summary>
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

    /// <summary>
    /// 몸통 중심 주변에서 Player·Turret을 찾아 대상별로 한 번만 피해를 준다.
    /// </summary>
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

        // 피해 수신자의 원본 계약만 사용합니다. Player 전용 분기를 두지 않습니다.
        IDamageableTarget damageable = targetTransform.GetComponentInParent<IDamageableTarget>();
        damageable?.ApplyDamage(explosionDamage);
    }

    /// <summary>
    /// Bomber 폭발 클립 마지막의 Animation Event에서 호출한다.
    /// </summary>
    public void OnExplosionAnimationFinished()
    {
        if (!_hasExploded || _completionReported || !isActiveAndEnabled || !HasAbilityAuthority)
            return;

        if (!CanAdvanceAbility)
        {
            _removalDeferred = true;
            return;
        }

        _removalDeferred = false;
        _completionReported = true;
        RemovalRequested?.Invoke();
    }

    private void Update()
    {
        if (_removalDeferred && CanAdvanceAbility)
            OnExplosionAnimationFinished();
    }

    /// <summary>
    /// 몸통 콜라이더의 중심을 폭발 위치로 사용하고 참조가 없으면 캡슐 또는 현재 위치를 사용한다.
    /// </summary>
    private Vector3 GetExplosionCenter()
    {
        Collider2D bodyCollider =
            _bodyCollider != null ? _bodyCollider : FindBodyCollider();

        if (bodyCollider != null)
            return bodyCollider.bounds.center;

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetExplosionCenter(), explosionRadius);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 모션 동안 이동을 잠그고 타격 Animation Event에서 한 번만 근접 피해를 전달한다.
/// 기존 근접 능력의 사거리와 공통 피해 계약을 재사용한다.
/// </summary>
public sealed class BossCloseRangeAbility : EnemyCloseRangeAbility
{
    [Header("정면 휘두르기")]
    [SerializeField, Min(0.1f)] private float sweepRadius = 3f;
    [SerializeField, Range(1f, 360f)] private float sweepAngle = 180f;
    [SerializeField] private LayerMask damageLayers;

    private readonly List<Collider2D> _hits = new(32);
    private readonly HashSet<TargetableComponent> _damaged = new();
    private ContactFilter2D _filter;
    private Vector2 _attackDirection;
    private bool _isExecuting;
    private bool _hitPending;
    private bool _hitDeferred;
    private bool _finishDeferred;

    public override bool IsExecuting => _isExecuting;

    protected override void Awake()
    {
        base.Awake();
        _filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = damageLayers };
    }

    /// <summary>필드 정지 중 보류한 타격과 종료를 재개 시 순서대로 처리한다.</summary>
    private void Update()
    {
        if (!CanAdvanceAbility)
            return;

        if (_hitDeferred)
            OnBossMeleeHit();
        if (_finishDeferred)
            OnBossMeleeAttackFinished();
    }

    /// <summary>사망이나 비활성화 이후 도착한 모션 이벤트가 피해를 만들지 않게 한다.</summary>
    private void OnDisable()
    {
        ResetAttack();
    }

    /// <summary>공격을 시작한 서버만 정면 범위의 유효 대상을 한 번씩 타격한다.</summary>
    public void OnBossMeleeHit()
    {
        if (!isActiveAndEnabled || !HasAbilityAuthority || !_isExecuting || !_hitPending)
            return;

        if (!CanAdvanceAbility)
        {
            _hitDeferred = true;
            return;
        }

        _hitDeferred = false;
        _hitPending = false;

        // 시작 시 고정한 방향으로 휘둘러 정면의 대상을 맞힌다. 뒤로 피하면 범위에서 벗어난다.
        Vector2 origin = _bodyCollider != null ? (Vector2)_bodyCollider.bounds.center : (Vector2)transform.position;
        _hits.Clear();
        _damaged.Clear();
        _filter.layerMask = damageLayers;
        Physics2D.OverlapCircle(origin, sweepRadius, _filter, _hits);
        float minimumDot = Mathf.Cos(sweepAngle * .5f * Mathf.Deg2Rad);
        foreach (var hit in _hits)
        {
            if (!CanAdvanceAbility || !_isExecuting) break;
            if (hit == null) continue;
            var target = hit.GetComponentInParent<TargetableComponent>();
            if (target == null || !target.CanBeTargeted || _damaged.Contains(target)) continue;
            Vector2 offset = hit.ClosestPoint(origin) - origin;
            if (offset.sqrMagnitude > .0001f && Vector2.Dot(offset.normalized, _attackDirection) < minimumDot) continue;
            _damaged.Add(target);
            ApplyDamage(target);
        }
    }

    /// <summary>공격 모션 종료 시 잠금을 해제하고 다음 이동과 능력 선택을 허용한다.</summary>
    public void OnBossMeleeAttackFinished()
    {
        if (!isActiveAndEnabled || !HasAbilityAuthority || !_isExecuting)
            return;

        if (!CanAdvanceAbility)
        {
            _finishDeferred = true;
            return;
        }

        // 정지 중 두 이벤트를 모두 받았다면 타격을 먼저 처리한다.
        if (_hitDeferred)
            OnBossMeleeHit();

        ResetAttack();
    }

    /// <summary>피해를 즉시 주지 않고 현재 대상을 향한 공격 방향을 고정해 모션 이벤트를 기다린다.</summary>
    protected override bool Activate()
    {
        if (!TryGetTarget(out TargetableComponent target) || !target.CanBeTargeted)
            return false;

        Vector2 origin = _bodyCollider != null ? (Vector2)_bodyCollider.bounds.center : (Vector2)transform.position;
        _attackDirection = ((Vector2)target.transform.position - origin).normalized;
        if (_attackDirection.sqrMagnitude < .0001f) _attackDirection = Vector2.down;
        _isExecuting = true;
        _hitPending = true;
        _hitDeferred = false;
        _finishDeferred = false;
        return true;
    }

    /// <summary>현재 공격과 미처리 이벤트 상태를 비운다.</summary>
    private void ResetAttack()
    {
        _isExecuting = false;
        _hitPending = false;
        _hitDeferred = false;
        _finishDeferred = false;
    }
}

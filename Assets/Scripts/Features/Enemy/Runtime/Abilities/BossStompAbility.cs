using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>타깃 위치를 원으로 예고하고, 필드 시각의 예고 종료 시 범위 피해를 적용한다.</summary>
public sealed class BossStompAbility : EnemyAbility
{
    // Inspector 설정과 제작 참조
    [Header("참조")]
    [SerializeField] private EnemyTargeting enemyTargeting;
    [SerializeField] private BossSkillNetworkView skillView;

    [Header("범위 공격")]
    [SerializeField, Min(1)] private int damage = 20;
    [SerializeField, Min(0.1f)] private float radius = 3f;
    [SerializeField, Min(0.1f)] private float warningDuration = 1.2f;
    [SerializeField] private LayerMask damageLayers;
    [SerializeField, Min(1f)] private float playerSearchRadius = 15f;

    // 실행 중 참조와 상태
    private readonly List<Collider2D> _hits = new(32);
    private readonly HashSet<TargetableComponent> _damaged = new();
    private ContactFilter2D _filter;
    private ContactFilter2D _playerFilter;
    private Vector2 _warningCenter;
    private double _damageAt;
    private int _waveCount = 1;
    private int _remainingWaves;
    private bool _isExecuting;
    private bool _warningActive;
    private bool _animationFinished;

    public override bool IsExecuting => _isExecuting;

    // Unity 생명주기
    protected override void Awake()
    {
        base.Awake();
        if (enemyTargeting == null) enemyTargeting = GetComponent<EnemyTargeting>();
        if (skillView == null) skillView = GetComponent<BossSkillNetworkView>();
        _filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = damageLayers };
        _playerFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true,
            layerMask = LayerMask.GetMask("PlayerHurtbox", "PlayerFeetBox") };
    }

    private void Update()
    {
        if (!_isExecuting || !CanAdvanceAbility)
            return;

        if (_warningActive && FieldSimulationTime.Seconds(this) >= _damageAt)
        {
            // 재진입이나 중복 이벤트가 이전 타격을 다시 실행하지 않게 먼저 소비한다.
            _warningActive = false;
            skillView.HideWarning();
            skillView.PlayGroundRupture(_warningCenter, radius);
            _remainingWaves--;
            ApplyAreaDamage();
            if (!_isExecuting || !isActiveAndEnabled)
                return;

            if (_remainingWaves > 0 && CheckCondition()) BeginWarning();
            else _remainingWaves = 0;
        }

        if (!_warningActive && _remainingWaves == 0 && _animationFinished)
            _isExecuting = false;
    }

    private void OnDisable()
    {
        _isExecuting = false;
        _warningActive = false;
        _remainingWaves = 0;
        if (skillView != null) skillView.HideWarning();
    }

    // 외부 호출과 상태 변경
    /// <summary>실행 전에 이번 공격의 예고 횟수를 지정한다.</summary>
    public void SetWaveCount(int count)
    {
        if (!_isExecuting) _waveCount = Mathf.Max(1, count);
    }

    public void SetWarningDuration(float duration)
    {
        if (!_isExecuting) warningDuration = Mathf.Max(.1f, duration);
    }

    public override bool CheckCondition() =>
        skillView != null && skillView.IsSpawned && skillView.IsConfigured &&
        enemyTargeting != null && enemyTargeting.TargetComponent != null &&
        enemyTargeting.TargetComponent.CanBeTargeted && damageLayers.value != 0;

    /// <summary>원거리에도 예고를 놓으므로 일반 공격의 거리 결과는 사용하지 않는다.</summary>
    public override bool CheckCondition(bool isInTargetRange) => CheckCondition();

    /// <summary>Stomp 클립 종료 직전의 Animation Event에서 호출한다.</summary>
    public void OnStompAnimationFinished()
    {
        if (isActiveAndEnabled && HasAbilityAuthority && _isExecuting)
            _animationFinished = true;
    }

    protected override bool Activate()
    {
        if (!CheckCondition()) return false;
        _isExecuting = true;
        _animationFinished = false;
        _remainingWaves = _waveCount;
        BeginWarning();
        return true;
    }

    // 내부 처리
    private void BeginWarning()
    {
        _warningCenter = FindWarningPosition();
        _damageAt = FieldSimulationTime.Seconds(this) + warningDuration;
        _warningActive = true;
        skillView.ShowWarning(_warningCenter, radius);
    }

    /// <summary>각 예고에서 가까운 유효 플레이어를 우선한다. 분대의 전투 타깃은 변경하지 않는다.</summary>
    private Vector2 FindWarningPosition()
    {
        Vector2 origin = transform.position;
        Vector2 result = enemyTargeting.Target.position;
        float nearest = float.PositiveInfinity;
        _hits.Clear();
        Physics2D.OverlapCircle(origin, playerSearchRadius, _playerFilter, _hits);
        foreach (var hit in _hits)
        {
            if (hit == null) continue;
            var player = hit.GetComponentInParent<TargetableComponent>();
            if (player == null || !player.CanBeTargeted || player.TargetType != TargetType.Player ||
                player.gameObject.scene != gameObject.scene) continue;
            float distance = ((Vector2)player.transform.position - origin).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            result = player.transform.position;
        }
        return result;
    }

    /// <summary>타격 순간에만 조회하며, 여러 콜라이더가 있는 대상도 한 번만 맞힌다.</summary>
    private void ApplyAreaDamage()
    {
        _hits.Clear();
        _damaged.Clear();
        // Inspector에서 실행 중 조정한 레이어도 다음 타격부터 사용한다.
        _filter.layerMask = damageLayers;
        Physics2D.OverlapCircle(_warningCenter, radius, _filter, _hits);
        foreach (var hit in _hits)
        {
            if (!CanAdvanceAbility || !_isExecuting) break;
            if (hit == null) continue;
            var target = hit.GetComponentInParent<TargetableComponent>();
            if (target == null || !target.CanBeTargeted || !_damaged.Add(target)) continue;
            target.TryApplyDamage(damage);
        }
    }
}

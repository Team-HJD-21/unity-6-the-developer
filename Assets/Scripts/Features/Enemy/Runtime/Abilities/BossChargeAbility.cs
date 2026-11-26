using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.AI;

/// <summary>방향을 예고한 뒤 직진한다. 물리 지형과 NavMesh 경계를 넘지 않으며 종료 후 빈틈을 둔다.</summary>
public sealed class BossChargeAbility : EnemyAbility
{
    private enum ChargeState { Windup, Charging, Recovery }

    [Header("참조")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D footCollider;
    [SerializeField] private EnemyTargeting enemyTargeting;
    [SerializeField] private EnemyAnimation enemyAnimation;
    [SerializeField] private BossSkillNetworkView skillView;

    [Header("돌진")]
    [SerializeField, Min(0.1f)] private float chargeSpeed = 10f;
    [SerializeField, Min(0.1f)] private float maxDistance = 8f;
    [SerializeField, Min(0f)] private float stoppingRange = .8f;
    [SerializeField, Min(0.1f)] private float windupDuration = .7f;
    [SerializeField, Min(0.1f)] private float followupWindupDuration = .45f;
    [SerializeField, Min(0f)] private float recoveryDuration = 1f;
    [SerializeField] private LayerMask blockingLayers;

    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[8];
    private EnemyMovement _movement;
    private NavMeshQueryFilter? _chargeNavigationFilter;
    private ContactFilter2D _blockingFilter;
    private ChargeState _state;
    private Vector2 _direction;
    private Vector2 _origin;
    private double _stateEndsAt;
    private int _chargeCount = 1;
    private int _remainingCharges;
    private bool _isExecuting;

    public override bool IsExecuting => _isExecuting;
    public override bool ControlsMovement => true;
    public bool IsInStoppingRange => enemyTargeting != null &&
        IsInRange(enemyTargeting.TargetColliders, stoppingRange);

    protected override void Awake()
    {
        base.Awake();
        if (body == null) body = GetComponent<Rigidbody2D>();
        _movement = GetComponent<EnemyMovement>();
        if (enemyTargeting == null) enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyAnimation == null) enemyAnimation = GetComponent<EnemyAnimation>();
        if (skillView == null) skillView = GetComponent<BossSkillNetworkView>();
        if (footCollider == null)
        {
            var foot = transform.Find("Foot");
            if (foot != null) footCollider = foot.GetComponent<Collider2D>();
        }
        _blockingFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = blockingLayers };
    }

    /// <summary>공통 컨트롤러의 제동과 겹치지 않게 능력에서만 돌진 물리를 처리한다.</summary>
    private void FixedUpdate()
    {
        if (!_isExecuting || !CanAdvanceAbility) return;
        double now = FieldSimulationTime.Seconds(this);
        if (_state == ChargeState.Windup)
        {
            body.linearVelocity = Vector2.zero;
            if (now < _stateEndsAt) return;
            skillView.HideChargeWarning();
            _state = ChargeState.Charging;
            _origin = body.position;
            _stateEndsAt = now + maxDistance / chargeSpeed;
        }
        if (_state == ChargeState.Charging)
        {
            float distance = chargeSpeed * Time.fixedDeltaTime;
            float clearance = distance + .05f;
            bool blocked = IsNavMeshBlocked(_direction * clearance) ||
                footCollider.Cast(_direction, _blockingFilter, _castHits, clearance) > 0;
            if (blocked || now >= _stateEndsAt || (body.position - _origin).sqrMagnitude >= maxDistance * maxDistance || IsInStoppingRange)
            {
                BeginRecovery();
                return;
            }
            Vector2 desiredVelocity = _direction * chargeSpeed;
            Vector2 delta = desiredVelocity * (1f + body.linearDamping * Time.fixedDeltaTime) - body.linearVelocity;
            body.AddForce(delta * (body.mass / Time.fixedDeltaTime), ForceMode2D.Force);
            enemyAnimation.SetFacingDirection(_direction);
            enemyAnimation.PlayMovementAnimation(chargeSpeed, chargeSpeed);
        }
        else if (_state == ChargeState.Recovery)
        {
            body.linearVelocity = Vector2.zero;
            if (now < _stateEndsAt) return;
            if (_remainingCharges > 0 && CheckCondition() && !IsInStoppingRange)
                BeginWindup(followupWindupDuration);
            else FinishCharge();
        }
    }

    private void OnDisable()
    {
        if (_isExecuting && body != null) body.linearVelocity = Vector2.zero;
        FinishCharge();
        if (skillView != null) skillView.HideChargeWarning();
    }

    public void SetChargeCount(int count)
    {
        if (!_isExecuting) _chargeCount = Mathf.Clamp(count, 1, 2);
    }

    public override bool CheckCondition() => body != null && body.simulated &&
        (body.bodyType == RigidbodyType2D.Dynamic || _movement?.CanHandMovementToPhysics(body) == true) &&
        (body.constraints & RigidbodyConstraints2D.FreezePosition) == 0 && footCollider != null && footCollider.enabled &&
        enemyTargeting != null && enemyTargeting.TargetComponent != null && enemyTargeting.TargetComponent.CanBeTargeted &&
        enemyAnimation != null && skillView != null && skillView.IsSpawned && skillView.IsChargeConfigured;

    public override bool CheckCondition(bool isInTargetRange) => CheckCondition();

    protected override bool Activate()
    {
        if (!CheckCondition()) return false;
        _chargeNavigationFilter = _movement != null && _movement.UsesNavMeshAgent
            ? _movement.GetNavigationFilter(gameObject.scene)
            : null;
        _movement?.BeginPhysicsMovement(body);
        _isExecuting = true;
        _remainingCharges = _chargeCount;
        BeginWindup(windupDuration);
        return true;
    }

    private bool IsNavMeshBlocked(Vector2 offset)
    {
        Vector3 position = body.position;
        if (_chargeNavigationFilter is NavMeshQueryFilter filter)
        {
            return !NavMesh.SamplePosition(position, out var origin, .3f, filter) ||
                   NavMesh.Raycast(origin.position, origin.position + (Vector3)offset, out _, filter);
        }

        return !NavMesh.SamplePosition(position, out var start, .3f, NavMesh.AllAreas) ||
               NavMesh.Raycast(start.position, start.position + (Vector3)offset, out _, NavMesh.AllAreas);
    }

    private void FinishCharge()
    {
        _isExecuting = false;
        _movement?.EndPhysicsMovement();
    }

    private void BeginWindup(float duration)
    {
        body.linearVelocity = Vector2.zero;
        _direction = ((Vector2)enemyTargeting.Target.position - body.position).normalized;
        if (_direction.sqrMagnitude < .0001f) _direction = Vector2.down;
        _state = ChargeState.Windup;
        _stateEndsAt = FieldSimulationTime.Seconds(this) + duration;
        enemyAnimation.SetFacingDirection(_direction);
        enemyAnimation.StopMovementAnimation();
        skillView.ShowChargeWarning(body.position, body.position + _direction * maxDistance);
    }

    private void BeginRecovery()
    {
        body.linearVelocity = Vector2.zero;
        _remainingCharges--;
        _state = ChargeState.Recovery;
        _stateEndsAt = FieldSimulationTime.Seconds(this) + recoveryDuration;
        enemyAnimation.StopMovementAnimation();
        skillView.HideChargeWarning();
    }
}

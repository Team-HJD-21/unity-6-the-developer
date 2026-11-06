// 원거리 적의 발동 조건과 탄환 생성 요청을 관리합니다.
using UnityEngine;

public sealed partial class EnemyProjectileAbility : EnemyAbility
{
    [Header("참조")]
    [SerializeField] private EnemyTargeting enemyTargeting;
    [SerializeField] private EnemyProjectile projectilePrefab;
    internal EnemyProjectile CheckpointProjectilePrefab => projectilePrefab;
    [SerializeField] private EnemyProjectileSpawner projectileSpawner;

    [Header("공격 설정")]
    [SerializeField, Min(1)] private int attackDamage = 5;
    [SerializeField, Min(0f)] private float fireOffset = 0.3f;

    [Header("발사 타이밍")]
    [Tooltip("공격 클립의 발사·종료 Animation Event를 사용하는 프리팹에서만 켠다.")]
    [SerializeField] private bool fireOnAnimationEvent;

    private bool _isExecuting;
    private bool _shotPending;
    private Vector2 _pendingDirection;
    private bool _fireEventDeferred;
    private bool _finishEventDeferred;

    public override bool IsExecuting => _isExecuting;

    protected override void Awake()
    {
        base.Awake();

        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        if (projectileSpawner == null)
            projectileSpawner = GetComponent<EnemyProjectileSpawner>();
    }

    public override bool CheckCondition()
    {
        return projectileSpawner != null &&
               projectilePrefab != null &&
               attackDamage > 0 &&
               TryGetTarget(out _) &&
               IsInRange(enemyTargeting.TargetColliders, activationRange);
    }

    protected override bool Activate()
    {
        if (projectilePrefab == null ||
            !TryGetTarget(out TargetableComponent target))
        {
            return false;
        }

        Vector2 direction =
            ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        if (fireOnAnimationEvent)
        {
            // 공격 방향을 고정하고, 실제 생성은 공격 모션의 발사 프레임에 맡긴다.
            _pendingDirection = direction;
            _shotPending = true;
            _isExecuting = true;
            return true;
        }

        return FireProjectile(direction);
    }

    /// <summary>공격 모션의 발사 프레임에서 예약된 투사체를 한 번 생성한다.</summary>
    public void OnProjectileFireAnimationEvent()
    {
        // 클라이언트의 연출 이벤트와 중복 이벤트는 새 투사체를 만들지 않는다.
        if (!isActiveAndEnabled || !HasAbilityAuthority || !fireOnAnimationEvent || !_isExecuting || !_shotPending)
            return;

        // 정지 전환 프레임에 도착한 이벤트를 버리지 않고 재개 후 한 번만 실행합니다.
        if (!CanAdvanceAbility)
        {
            _fireEventDeferred = true;
            return;
        }

        _fireEventDeferred = false;
        _shotPending = false;
        FireProjectile(_pendingDirection);
    }

    /// <summary>공격 모션이 끝나면 이동과 다음 능력 사용을 허용한다.</summary>
    public void OnProjectileAttackAnimationFinished()
    {
        if (!fireOnAnimationEvent || !_isExecuting || !isActiveAndEnabled || !HasAbilityAuthority)
            return;
        if (!CanAdvanceAbility)
        {
            _finishEventDeferred = true;
            return;
        }
        // 같은 정지 구간에 발사/종료가 모두 도착했다면 발사를 먼저 정리합니다.
        if (_fireEventDeferred)
            OnProjectileFireAnimationEvent();
        CancelPendingAttack();
    }

    private void Update()
    {
        if (!CanAdvanceAbility)
            return;
        if (_fireEventDeferred)
            OnProjectileFireAnimationEvent();
        if (_finishEventDeferred)
            OnProjectileAttackAnimationFinished();
    }

    private void OnDisable()
    {
        CancelPendingAttack();
    }

    private void CancelPendingAttack()
    {
        _shotPending = false;
        _isExecuting = false;
        _fireEventDeferred = false;
        _finishEventDeferred = false;
    }

    private bool FireProjectile(Vector2 direction)
    {
        if (!CanAdvanceAbility || projectilePrefab == null || projectileSpawner == null)
            return false;

        Vector3 spawnPosition =
            transform.position + (Vector3)(direction * fireOffset);

        return projectileSpawner.TrySpawn(
            projectilePrefab,
            spawnPosition,
            direction,
            attackDamage, this);
    }

    private bool TryGetTarget(out TargetableComponent target)
    {
        target = null;

        if (enemyTargeting == null || enemyTargeting.Target == null)
            return false;

        target = enemyTargeting.Target.GetComponent<TargetableComponent>();

        return target != null && target.CanBeTargeted;
    }
}

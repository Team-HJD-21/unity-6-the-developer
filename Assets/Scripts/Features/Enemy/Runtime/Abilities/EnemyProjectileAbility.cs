using UnityEngine;

/// <summary>
/// 타깃 방향으로 투사체를 발사하고 애니메이션 이벤트 발사를 지원한다.
/// </summary>
public sealed partial class EnemyProjectileAbility : EnemyAbility
{
    // Inspector 설정과 제작 참조

    [Header("참조")]
    [SerializeField] private EnemyTargeting enemyTargeting;
    [SerializeField] private EnemyDamageProjectile projectilePrefab;
    [SerializeField] private EnemyProjectileGenerator projectileSpawner;

    [Header("공격 설정")]
    [SerializeField, Min(1)] private int attackDamage = 5;
    [SerializeField, Min(0f)] private float fireOffset = 0.3f;

    [Header("발사 타이밍")]
    [Tooltip("공격 클립의 발사·종료 Animation Event를 사용하는 프리팹에서만 켠다.")]
    [SerializeField] private bool fireOnAnimationEvent;

    // 실행 중 참조와 상태
    private bool _isExecuting;
    private bool _shotPending;
    private Vector2 _pendingDirection;
    private bool _fireEventDeferred;
    private bool _finishEventDeferred;

    internal EnemyDamageProjectile CheckpointProjectilePrefab => projectilePrefab;

    public override bool IsExecuting => _isExecuting;

    // Unity 생명주기

    /// <summary>배정 타깃과 투사체 생성기 참조를 연결한다.</summary>
    protected override void Awake()
    {
        base.Awake();

        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        if (projectileSpawner == null)
            projectileSpawner = GetComponent<EnemyProjectileGenerator>();
    }

    /// <summary>필드가 재개되면 보류한 발사와 공격 종료 이벤트를 순서대로 처리한다.</summary>
    private void Update()
    {
        if (!CanAdvanceAbility)
            return;
        if (_fireEventDeferred)
            OnProjectileFireAnimationEvent();
        if (_finishEventDeferred)
            OnProjectileAttackAnimationFinished();
    }

    /// <summary>아직 실행하지 않은 발사 요청과 공격 잠금을 해제한다.</summary>
    private void OnDisable()
    {
        CancelPendingAttack();
    }

    // 외부 호출과 상태 변경

    /// <summary>
    /// 발사 구성이 유효하고 현재 타깃이 발동 범위 안에 있는지 확인한다.
    /// </summary>
    public override bool CheckCondition()
    {
        return enemyTargeting != null &&
               CheckCondition(IsInRange(enemyTargeting.TargetColliders));
    }

    /// <summary>이번 행동에서 계산한 거리 결과로 발사 조건을 확인한다.</summary>
    public override bool CheckCondition(bool isInTargetRange)
    {
        return isInTargetRange &&
               projectileSpawner != null &&
               projectilePrefab != null &&
               attackDamage > 0 &&
               TryGetTarget(out _);
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

    // 파생 클래스 공통 처리

    /// <summary>
    /// 타깃 방향을 정해 즉시 발사하거나 공격 애니메이션의 발사 이벤트를 예약한다.
    /// </summary>
    protected override bool Activate()
    {
        if (projectilePrefab == null ||
            !TryGetTarget(out TargetableComponent target) ||
            !target.CanBeTargeted)
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

    // 내부 처리

    /// <summary>
    /// 예약된 발사와 정지 중 지연된 이벤트를 비우고 다음 행동을 허용한다.
    /// </summary>
    private void CancelPendingAttack()
    {
        _shotPending = false;
        _isExecuting = false;
        _fireEventDeferred = false;
        _finishEventDeferred = false;
    }

    /// <summary>
    /// 발사 방향으로 생성 위치를 보정한 뒤 생성기에 피해량과 생성 주체를 전달한다.
    /// </summary>
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

    /// <summary>타깃 지정 시 저장한 컴포넌트 참조를 반환한다.</summary>
    private bool TryGetTarget(out TargetableComponent target)
    {
        target = enemyTargeting != null
            ? enemyTargeting.TargetComponent
            : null;

        return target != null;
    }
}

using UnityEngine;

enum SummonState
{
    Mobile,
    Transforming,
    Transformed
}

public class EnemySummonAbility : EnemyAbility
{
    [Header("참조")]
    [SerializeField] private EnemyTargeting enemyTargeting;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField] private EnemySummonSpawner summonSpawner;
    [SerializeField] private EnemyDefinition summonDefinition;

    [Header("소환 설정")]
    [SerializeField, Min(1)] private int summonCount = 1;
    [SerializeField, Min(0)] private int deathSummonCount = 5;

    private SummonState _state = SummonState.Mobile;
    private bool _isSummoning;
    private bool _summonPending;

    public override bool IsExecuting =>
        _state == SummonState.Transforming || _isSummoning;

    protected override void Awake()
    {
        base.Awake();

        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();
        if (rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
        if (summonSpawner == null)
            summonSpawner = GetComponent<EnemySummonSpawner>();
    }

    public override bool CheckCondition()
    {
        if (_state == SummonState.Transformed)
            return true;

        if (_state != SummonState.Mobile ||
            enemyTargeting == null ||
            enemyTargeting.Target == null)
        {
            return false;
        }

        ITargetable target = enemyTargeting.Target.GetComponent<ITargetable>();

        return target != null &&
               target.CanBeTargeted &&
               IsInRange(enemyTargeting.TargetColliders);
    }

    protected override bool Activate()
    {
        if (_state == SummonState.Mobile)
        {
            _state = SummonState.Transforming;
            return true;
        }

        if (_state == SummonState.Transformed &&
            summonSpawner != null &&
            summonDefinition != null &&
            summonDefinition.Prefab != null &&
            summonCount > 0)
        {
            _isSummoning = true;
            _summonPending = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 변태 애니메이션이 끝나면 위치를 고정하고 소환 상태로 전환한다.
    /// </summary>
    public void OnTransformationAnimationFinished()
    {
        if (_state != SummonState.Transforming)
            return;

        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.constraints |= RigidbodyConstraints2D.FreezePosition;
        _state = SummonState.Transformed;
    }

    /// <summary>소환 모션의 방출 프레임에서 예약된 몬스터를 한 번 생성한다.</summary>
    public void OnSummonAnimationEvent()
    {
        // 실행을 예약하지 않은 클라이언트와 중복 이벤트는 생성하지 않는다.
        if (!isActiveAndEnabled ||
            _state != SummonState.Transformed ||
            !_isSummoning || !_summonPending)
        {
            return;
        }

        _summonPending = false;

        if (summonSpawner != null && summonDefinition != null)
        {
            summonSpawner.Spawn(
                summonDefinition,
                summonCount,
                enemyTargeting != null ? enemyTargeting.Target : null);
        }
    }

    /// <summary>소환 모션이 끝나면 다음 능력 사용을 허용한다.</summary>
    public void OnSummonAnimationFinished()
    {
        _isSummoning = false;
        _summonPending = false;
    }

    private void OnDisable()
    {
        _isSummoning = false;
        _summonPending = false;
    }
}

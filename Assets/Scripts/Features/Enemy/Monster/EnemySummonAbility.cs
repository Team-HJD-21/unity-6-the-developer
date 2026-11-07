using UnityEngine;

public class EnemySummonAbility : EnemyAbility
{
    [Header("참조")]
    [SerializeField] private EnemyTargeting enemyTargeting;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemySummonSpawner summonSpawner;
    [SerializeField] private EnemyDefinition summonDefinition;

    [Header("소환 설정")]
    [SerializeField, Min(1)] private int summonCount = 1;
    [SerializeField, Min(0)] private int deathSummonCount = 5;

    private bool _isSummoning;
    private bool _summonPending;

    public override bool IsExecuting => _isSummoning;

    protected override void Awake()
    {
        base.Awake();

        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();
        if (summonSpawner == null)
            summonSpawner = GetComponent<EnemySummonSpawner>();
    }

    public override bool CheckCondition()
    {
        return summonSpawner != null &&
               summonDefinition != null &&
               summonDefinition.Prefab != null &&
               summonCount > 0;
    }

    protected override bool Activate()
    {
        _isSummoning = true;
        _summonPending = true;
        return true;
    }

    /// <summary>소환 모션의 방출 프레임에서 예약된 몬스터를 한 번 생성한다.</summary>
    public void OnSummonAnimationEvent()
    {
        // 실행을 예약하지 않은 클라이언트와 중복 이벤트는 생성하지 않는다.
        if (!isActiveAndEnabled ||
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

    /// <summary>파괴 시 쿨타임과 모션 대기 없이 추가 몬스터를 생성한다.</summary>
    public void SummonOnDeath()
    {
        _isSummoning = false;
        _summonPending = false;

        if (summonSpawner == null ||
            summonDefinition == null ||
            summonDefinition.Prefab == null ||
            deathSummonCount <= 0)
        {
            return;
        }

        summonSpawner.Spawn(
            summonDefinition,
            deathSummonCount,
            enemyTargeting != null ? enemyTargeting.Target : null);
    }

    private void OnDisable()
    {
        _isSummoning = false;
        _summonPending = false;
    }
}

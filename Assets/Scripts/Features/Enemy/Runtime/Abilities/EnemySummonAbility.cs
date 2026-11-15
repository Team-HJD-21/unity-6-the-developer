using UnityEngine;

/// <summary>
/// 소환 모션의 실행 상태를 관리하고 방출 이벤트와 사망 시 적을 소환한다.
/// </summary>
public class EnemySummonAbility : EnemyAbility
{
    // Inspector 설정과 제작 참조

    [Header("참조")]
    [SerializeField] private EnemyTargeting enemyTargeting;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemySummonGenerator summonSpawner;
    [SerializeField] private EnemyDefinition summonDefinition;

    [Header("소환 설정")]
    [SerializeField, Min(1)] private int summonCount = 1;
    [SerializeField, Min(0)] private int deathSummonCount = 5;

    // 실행 중 참조와 상태
    private bool _isSummoning;
    private bool _summonPending;

    public override bool IsExecuting => _isSummoning;

    // Unity 생명주기

    /// <summary>소환에 필요한 타깃·체력·생성기 참조를 연결한다.</summary>
    protected override void Awake()
    {
        base.Awake();

        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();
        if (summonSpawner == null)
            summonSpawner = GetComponent<EnemySummonGenerator>();
    }

    /// <summary>소환 모션의 실행 상태와 미처리 방출 요청을 해제한다.</summary>
    private void OnDisable()
    {
        _isSummoning = false;
        _summonPending = false;
    }

    // 외부 호출과 상태 변경

    /// <summary>다음 소환 모션의 수량을 지정한다. 진행 중인 소환의 수량은 바꾸지 않는다.</summary>
    public void SetSummonCount(int count)
    {
        if (!_isSummoning)
            summonCount = Mathf.Max(1, count);
    }

    /// <summary>
    /// 생성기와 소환 정의가 준비되어 있는지 확인한다. 변태와 거리 판단은 컨트롤러가 담당한다.
    /// </summary>
    public override bool CheckCondition()
    {
        return summonSpawner != null &&
               summonDefinition != null &&
               summonDefinition.Prefab != null &&
               summonCount > 0;
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

    // 파생 클래스 공통 처리

    /// <summary>
    /// 소환 실행을 잠그고 애니메이션 방출 이벤트에서 사용할 생성 요청을 예약한다.
    /// </summary>
    protected override bool Activate()
    {
        _isSummoning = true;
        _summonPending = true;
        return true;
    }
}

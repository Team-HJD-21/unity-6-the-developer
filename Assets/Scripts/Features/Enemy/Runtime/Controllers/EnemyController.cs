using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 몬스터의 공통 참조와 타깃 지정 기능을 제공하고, 행동 실행은 자식 클래스에 맡긴다.
/// </summary>
public abstract class EnemyController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] protected EnemyTargeting  enemyTargeting;
    [SerializeField] protected EnemyAnimation enemyAnimation;

    [Header("능력")]
    [FormerlySerializedAs("enemyCombat")]
    [SerializeField] protected EnemyAbility enemyAbility;
    private EnemySquad _squad;
    
    /// <summary>
    /// 다른 미사일이 이미 이 적을 목표로 삼았는지 나타낸다.
    /// 미사일 시스템은 중복 목표 배정을 막기 위해 이 값을 확인하고 설정한다.
    /// </summary>
    public bool IsTargeted { get; set; }

    /// <summary>Definition에서 최초 등록 시 전달받은 공격 인원 집계 여부를 반환한다.</summary>
    public bool IsIncludedInAttackerLimit { get; private set; } = true;
    
    /// <summary>
    /// 자식 클래스가 공유하는 타깃·애니메이션·능력 참조를 초기화한다.
    /// </summary>
    protected virtual void Awake()
    {
        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyAnimation == null)
            enemyAnimation = GetComponent<EnemyAnimation>();
        if (enemyAbility == null)
            enemyAbility = GetComponent<EnemyAbility>();
    }

    /// <summary>
    /// 몬스터 유형에 맞는 행동을 실행한다.
    /// </summary>
    public abstract void ExecuteBehavior();

    /// <summary>최초 분대 등록 시 공격 인원 집계 설정을 적용한다.</summary>
    /// <param name="squad">소속될 분대. 이미 다른 분대에 소속되어 있으면 등록하지 않는다.</param>
    /// <param name="isIncludedInAttackerLimit">이 몬스터를 타깃별 인원에 포함할지 여부.</param>
    /// <returns>등록에 성공했거나 이미 같은 분대이면 true.</returns>
    public bool JoinSquad(EnemySquad squad, bool isIncludedInAttackerLimit = true)
    {
        if (squad == null)
            return false;
        // 소속 중에는 분대와 집계 설정을 바꾸지 않아 기존 배정과 불일치하지 않게 한다.
        if (_squad != null)
            return _squad == squad;

        IsIncludedInAttackerLimit = isIncludedInAttackerLimit;
        if (!squad.AddMember(this))
            return false;

        _squad = squad;
        return true;
    }

    /// <summary>분대 갱신을 요청하며 실제 검색은 대표 구성원만 실행한다.</summary>
    public void RefreshTarget()
    {
        _squad?.RefreshTarget(this);
    }

    /// <summary>감지한 플레이어를 소속 분대에 전달한다.</summary>
    public void NotifyPlayerDetected(ITargetable player)
    {
        if (!isActiveAndEnabled)
            return;

        _squad?.TryPrioritizePlayer(this, player);
    }

    /// <summary>
    /// 현재 분대의 구성원과 배정 인원을 정리하고 소속 참조를 비운다.
    /// </summary>
    public void LeaveSquad()
    {
        _squad?.RemoveMember(this);
        _squad = null;
    }

    /// <summary>체력으로 사망했을 때 네트워크 제거 전에 고유 반응을 실행한다.</summary>
    public virtual void OnDeath()
    {
    }

    /// <summary>
    /// 능력 발동에 성공하면 애니메이션을 재생한다.
    /// </summary>
    protected bool TryUseAbility()
    {
        if (enemyAbility == null || !enemyAbility.TryUse())
            return false;

        enemyAnimation.PlayAbilityAnimation();
        return true;
    }

    /// <summary>
    /// 타깃을 지정하고 자식 클래스의 후속 처리를 호출한다.
    /// </summary>
    /// <param name="target">배정할 타깃. null이면 기존 타깃을 해제한다.</param>
    public void AssignTarget(Transform target)
    {
        enemyTargeting.SetTarget(target);
        OnTargetChanged(target);
    }

    /// <summary>
    /// 타깃 지정 후 몬스터 유형에 필요한 후속 처리를 수행한다.
    /// </summary>
    /// <param name="target">배정된 타깃. 해제된 경우 null.</param>
    protected abstract void OnTargetChanged(Transform target);
}

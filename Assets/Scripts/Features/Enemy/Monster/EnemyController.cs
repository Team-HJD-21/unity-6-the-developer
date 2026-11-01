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
    
    /// <summary>
    /// 다른 미사일이 이미 이 적을 목표로 삼았는지 나타낸다.
    /// 미사일 시스템은 중복 목표 배정을 막기 위해 이 값을 확인하고 설정한다.
    /// </summary>
    public bool IsTargeted { get; set; }
    
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

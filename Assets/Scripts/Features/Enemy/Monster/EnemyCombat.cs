using UnityEngine;
using TeamHJD.Game.Presentation.Scene;

public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private EnemyTargeting enemyTargeting;
    [SerializeField] private EnemyAttack enemyAttack;
    
    [Header("공격 설정")]
    [SerializeField] private float attackRange = 0.4f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = 5;
    private double _nextAttackTime;
    private FieldSimulationActor _simulation;
    private double GameSeconds => (_simulation ??= GetComponent<FieldSimulationActor>())?.Seconds ?? Time.timeAsDouble;
    
    private Collider2D _bodyCollider;

    private void Awake()
    {
        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyAttack == null)
            enemyAttack = GetComponent<EnemyAttack>();
        
        // 공격 거리는 몬스터 몸통 콜라이더의 겉면부터 계산한다.
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            if (collider.isTrigger)
                continue;

            _bodyCollider = collider;
            break;
        }
    }
    
    /// <summary>
    /// 몬스터와 타겟의 활성 몸통 콜라이더 사이 거리가 공격 범위 안인지 확인한다.
    /// </summary>
    /// <returns>타겟이 공격 범위 안에 있으면 <see langword="true"/>.</returns>
    public bool IsInAttackRange()
    {
        if (_bodyCollider == null || !_bodyCollider.enabled || enemyTargeting.TargetColliders == null)
            return false;

        foreach (Collider2D targetCollider in enemyTargeting.TargetColliders)
        {
            // 방향 전환 등으로 비활성화된 콜라이더와 피격용 트리거는 제외한다.
            if (targetCollider == null || !targetCollider.enabled ||
                !targetCollider.gameObject.activeInHierarchy || targetCollider.isTrigger)
                continue;

            ColliderDistance2D distance = _bodyCollider.Distance(targetCollider);
            if (distance.isValid && distance.distance <= attackRange)
                return true;
        }

        return false;
    }

    public bool TryAttack()
    {
        _simulation ??= GetComponent<FieldSimulationActor>();
        // 공격 쿨타임이 남아 있으면 이번 공격을 건너뛴다.
        if ((_simulation != null && !_simulation.CanAdvance) || GameSeconds < _nextAttackTime)
            return false;

        if (enemyAttack == null || enemyTargeting.Target == null)
            return false;

        TargetableComponent targetable = enemyTargeting.Target.GetComponent<TargetableComponent>();
        if (targetable == null || !targetable.CanBeTargeted ||
            !enemyAttack.Execute(targetable, attackDamage))
            return false;

        _nextAttackTime = GameSeconds + attackCooldown;
        return true;
    }
}

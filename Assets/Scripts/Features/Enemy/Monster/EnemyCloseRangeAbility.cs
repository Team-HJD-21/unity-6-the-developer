using UnityEngine;

/// <summary>
/// 사거리 안의 플레이어나 터렛에 즉시 피해를 주는 근접 능력이다.
/// </summary>
public sealed class EnemyCloseRangeAbility : EnemyAbility
{
    [SerializeField] private EnemyTargeting enemyTargeting;
    
    [Header("공격 설정")]
    [SerializeField] private float attackRange = 0.4f;
    [SerializeField] private int attackDamage = 5;
    
    private Collider2D _bodyCollider;

    private void Awake()
    {
        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
        
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
    /// 근접 공격에 필요한 타깃과 몸통 사이의 거리를 확인한다.
    /// </summary>
    public override bool CheckCondition()
    {
        return attackDamage > 0 && TryGetTarget(out _) && IsInRange();
    }

    private bool IsInRange()
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

    protected override bool Activate()
    {
        if (!TryGetTarget(out TargetableComponent target))
            return false;

        if (target.TargetType == TargetType.Turret)
            return target.TryApplyDamage(attackDamage);

        if (target.TargetType != TargetType.Player)
            return false;

        PlayerInfo player = target.GetComponentInParent<PlayerInfo>();
        if (player == null)
            return false;

        player.TakeDamage(attackDamage);
        return true;
    }

    private bool TryGetTarget(out TargetableComponent target)
    {
        target = null;
        if (enemyTargeting == null || enemyTargeting.Target == null)
            return false;

        target = enemyTargeting.Target.GetComponent<TargetableComponent>();
        return target != null && target.CanBeTargeted;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D bodyCollider = _bodyCollider;

        if (bodyCollider == null)
        {
            foreach (Collider2D collider in GetComponents<Collider2D>())
            {
                if (!collider.isTrigger)
                {
                    bodyCollider = collider;
                    break;
                }
            }
        }

        if (bodyCollider == null)
            return;

        Bounds bounds = bodyCollider.bounds;
        float bodyRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bounds.center, bodyRadius + attackRange);
    }
}

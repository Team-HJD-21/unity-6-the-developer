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
    


    protected override void Awake()
    {
        base.Awake();

        if (enemyTargeting == null)
            enemyTargeting = GetComponent<EnemyTargeting>();
    }

    public override bool CheckCondition()
    {
        return attackDamage > 0 &&
               TryGetTarget(out _) &&
               IsInRange(enemyTargeting.TargetColliders, attackRange);
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

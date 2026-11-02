using UnityEngine;

public sealed class EnemyProjectileAbility : EnemyAbility
{
    [Header("참조")]
    [SerializeField] private EnemyTargeting enemyTargeting;
    [SerializeField] private EnemyProjectile projectilePrefab;
    [SerializeField] private EnemyProjectileSpawner projectileSpawner;
    
    [Header("공격 설정")]
    [SerializeField, Min(1)] private int attackDamage = 5;
    [SerializeField, Min(0f)] private float fireOffset = 0.3f;
    
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
        
        Vector3 spawnPosition =
            transform.position + (Vector3)(direction * fireOffset);
        
        return projectileSpawner.TrySpawn(
            projectilePrefab,
            spawnPosition,
            direction,
            attackDamage);
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

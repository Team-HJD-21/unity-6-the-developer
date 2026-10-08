using UnityEngine;

/// <summary>보조 포탑 사거리 안의 살아 있는 적을 검색합니다.</summary>
public static class PlayerSupportTurretTargeting
{
    public static Transform FindClosest(Vector3 origin, float attackRange, string enemyTag)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRange);
        float closestDistanceSqr = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (var hit in hits)
        {
            if (hit.CompareTag(enemyTag) || hit.transform.root.CompareTag(enemyTag))
            {
                // EnemyHealth 검사 (체력이 남아있는지 확인)
                EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null && enemyHealth.CurrentHealth > 0)
                {
                    float distSqr = (hit.transform.position - origin).sqrMagnitude;
                    if (distSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distSqr;
                        closestEnemy = hit.transform;
                    }
                    continue;
                }

                // Legacy 제거로 인해 로직 무효화: 구형 Monster 타깃 폴백.
                /*
                Monster monster = hit.GetComponentInParent<Monster>();
                if (monster != null && !monster.isDead)
                {
                    float distSqr = (monster.transform.position - origin).sqrMagnitude;
                    if (distSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distSqr;
                        closestEnemy = monster.transform;
                    }
                }
                */
            }
        }

        return closestEnemy;
    }
}

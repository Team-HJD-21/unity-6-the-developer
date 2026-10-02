using System.Collections;
using UnityEngine;

/// <summary>
/// 소환된 보조 포탑의 적 탐색, 조준 회전, 탄환 자동 발사 및 수명 관리 전담 컴포넌트
/// </summary>
public class TemporaryTurret : MonoBehaviour
{
    [Header("Bullet Settings")]
    [Tooltip("기존 플레이어 총알 프리팹 (PlayerBullet 컴포넌트 포함)")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("총알이 생성될 포구 위치")]
    [SerializeField] private Transform firePoint;

    [Tooltip("포탑 탄환 피해량")]
    [SerializeField] private float damagePerShot = 10f;

    [Tooltip("포탑 탄환 색상 (에메랄드 녹색)")]
    [SerializeField] private Color bulletColor = new Color(0.2f, 1f, 0.4f, 1f);

    [Header("Turret Attributes")]
    [Tooltip("포탑 작동 수명(초)")]
    [SerializeField] private float duration = 8f;

    [Tooltip("적 탐지 사거리 반경")]
    [SerializeField] private float attackRange = 7f;

    [Tooltip("발사 간격 (초당 약 3.3발)")]
    [SerializeField] private float fireRate = 0.3f;

    [Tooltip("회전할 포탑 헤드/배럴 트랜스폼 (미할당 시 본체 회전)")]
    [SerializeField] private Transform turretHead;

    [Header("Targeting")]
    [SerializeField] private string enemyTag = "Enemy";

    private Transform targetEnemy;
    private float fireTimer = 0f;

    private void Start()
    {
        if (firePoint == null) firePoint = transform;
        if (turretHead == null) turretHead = transform;

        // 수명 종료 시 자동 파괴
        StartCoroutine(LifeTimeRoutine());
    }

    private void Update()
    {
        FindClosestTarget();

        if (targetEnemy != null)
        {
            AimAtTarget();

            fireTimer += Time.deltaTime;
            if (fireTimer >= fireRate)
            {
                fireTimer = 0f;
                Shoot();
            }
        }
        else
        {
            fireTimer = 0f;
        }
    }

    /// <summary>
    /// 사거리 내에서 가장 가까운 적을 탐색 (EnemyHealth 우선, Monster 폴백)
    /// </summary>
    private void FindClosestTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
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
                    float distSqr = (hit.transform.position - transform.position).sqrMagnitude;
                    if (distSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distSqr;
                        closestEnemy = hit.transform;
                    }
                    continue;
                }

                // 레거시 Monster 검사
                Monster monster = hit.GetComponentInParent<Monster>();
                if (monster != null && !monster.isDead)
                {
                    float distSqr = (monster.transform.position - transform.position).sqrMagnitude;
                    if (distSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distSqr;
                        closestEnemy = monster.transform;
                    }
                }
            }
        }

        targetEnemy = closestEnemy;
    }

    /// <summary>
    /// 타겟 적을 향해 포탑 헤드 각도 회전
    /// </summary>
    private void AimAtTarget()
    {
        Vector2 dir = (targetEnemy.position - turretHead.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        turretHead.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// 기존 PlayerBullet을 재활용하여 탄환 발사
    /// </summary>
    private void Shoot()
    {
        if (bulletPrefab == null) return;

        Vector2 dir = (targetEnemy.position - firePoint.position).normalized;
        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        PlayerBullet bullet = bulletObj.GetComponent<PlayerBullet>();
        if (bullet != null)
        {
            bullet.InitBullet(WeaponType.DefaultShotgun, bulletColor, damagePerShot, dir, Vector2.zero);
        }
    }

    private IEnumerator LifeTimeRoutine()
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
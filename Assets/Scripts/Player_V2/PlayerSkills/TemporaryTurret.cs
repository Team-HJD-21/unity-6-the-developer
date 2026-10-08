using UnityEngine;

/// <summary>
/// 회수 가능한 보조 포탑의 적 탐색, 조준 회전 및 탄환 자동 발사를 담당합니다.
/// </summary>
public class TemporaryTurret : MonoBehaviour
{
    [Header("Bullet Settings")]
    [Tooltip("보조 포탑 전용 실탄 프리팹 (TemporaryTurretProjectile 컴포넌트 포함)")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("총알이 생성될 포구 위치")]
    [SerializeField] private Transform firePoint;

    [Tooltip("포탑 탄환 피해량")]
    [SerializeField, Min(0f)] private float damagePerShot = 6f;

    [Tooltip("포탑 탄환 색상")]
    [SerializeField] private Color bulletColor = Color.white;

    [Header("Turret Attributes")]
    [Tooltip("적 탐지 사거리 반경")]
    [SerializeField] private float attackRange = 21f;

    [Tooltip("발사 간격 (초당 약 3.3발)")]
    [SerializeField] private float fireRate = 0.3f;

    [Tooltip("회전할 포탑 헤드/배럴 트랜스폼 (미할당 시 본체 회전)")]
    [SerializeField] private Transform turretHead;

    [Header("Targeting")]
    [SerializeField] private string enemyTag = "Enemy";

    private Transform targetEnemy;
    private float fireTimer = 0f;
    private PlayerTurretSkill _owner;
    private bool _recovered;

    public void AssignOwner(PlayerTurretSkill owner) => _owner = owner;

    public bool TryRecover(PlayerTurretSkill owner)
    {
        if (_recovered || owner == null || _owner != owner || !owner.ReleaseTurret(this)) return false;
        _recovered = true;
        _owner = null;
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
        return true;
    }

    private void Start()
    {
        if (firePoint == null) firePoint = transform;
        if (turretHead == null) turretHead = transform;

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
    /// 설치된 캐논과 같은 외형의 전용 실탄을 실제 총구에서 발사합니다.
    /// </summary>
    private void Shoot()
    {
        if (bulletPrefab == null || targetEnemy == null || firePoint == null ||
            turretHead == null || !firePoint.IsChildOf(turretHead) ||
            bulletPrefab.GetComponent<TemporaryTurretProjectile>() == null) return;

        Vector2 dir = (targetEnemy.position - firePoint.position).normalized;
        if (!PlayerAimUtility.IsValidDirection(dir)) return;
        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        var bullet = bulletObj.GetComponent<TemporaryTurretProjectile>();
        if (!bullet.Initialize(dir, damagePerShot)) { PlayerFieldDevice.Dispose(bulletObj); return; }
        var renderer = bulletObj.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = bulletColor;
    }

    private void OnDestroy() { if (_owner != null) _owner.ReleaseTurret(this); }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

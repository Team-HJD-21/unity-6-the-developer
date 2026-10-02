using System.Collections;
using UnityEngine;

/// <summary>
/// 소환된 보조 포탑의 적 탐색, 조준 회전, 탄환 자동 발사 및 수명 관리 전담 컴포넌트
/// 
/// [채택 이유 및 대안 비교]
/// 1. 기존 'PlayerBullet' 100% 재활용:
///    - 대안: 포탑 전용 탄환 및 피격 로직 신규 작성.
///    - 채택 이유: 기존에 제작된 PlayerBullet.cs의 색상, 대미지, Enemy 태그 판정,
///      MonsterSlowDebuff 연동을 그대로 활용하여 코드 중복과 버그를 원천 차단했습니다.
/// 
/// 2. OverlapCircleAll 기반 고속 타겟팅:
///    - 매 프레임 모든 몬스터를 순회하는 대신, 탐지 반경 내의 Enemy 태그 오브젝트만
///      거리 계산(sqrMagnitude)을 통해 가장 가까운 적을 조준합니다.
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
    /// 사거리 내에서 가장 가까운 적을 탐색
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
            // 기본 산탄총 타입 속성으로 초기화 (원하면 CryoBlaster로 바꿔 슬로우 포탑으로도 사용 가능)
            bullet.InitBullet(WeaponType.DefaultShotgun, bulletColor, damagePerShot, dir, Vector2.zero);
        }
    }

    private IEnumerator LifeTimeRoutine()
    {
        yield return new WaitForSeconds(duration);

        // (선택) 여기에 자폭 작은 폭발 이펙트 추가 가능
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        // 사거리 기즈모 시각화
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
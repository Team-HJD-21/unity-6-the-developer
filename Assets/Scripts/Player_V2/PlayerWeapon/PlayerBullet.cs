using UnityEngine;

public enum WeaponType
{
    DefaultShotgun, // 1번: 기본 산탄총 (일반 피해)
    CryoBlaster     // 2번: 감속 냉각총 (감속 디버프 부여)
}

/// <summary>
/// 플레이어 발사 탄환의 이동 궤적, 수명 관리, 태그 기반 충돌 판정 및 디버프 주입 컴포넌트
/// 
/// [채택 이유 및 대안 비교]
/// 1. 단일 프리팹 멀티 무기 색상/속성 주입:
///    - 대안: 무기마다 별도의 총알 프리팹을 따로 생성하여 관리.
///    - 채택 이유: 프로토타입 단계에서 불필요한 프리팹 에셋 증식을 막고, InitBullet()을 통해
///      런타임에 색상(SpriteRenderer.color)과 피해량, 디버프 속성을 주입하여 유지보수성을 극대화했습니다.
/// 
/// 2. 'Enemy' 태그 1차 필터링 후 컴포넌트 접근:
///    - 대안: 모든 충돌체에 GetComponent<Monster>()를 매번 호출.
///    - 채택 이유: 매 충돌마다 GetComponent를 실행하면 불필요한 GC 및 CPU 부하가 발생합니다.
///      기존 규칙에 맞춰 target.CompareTag("Enemy")로 1차 유효성을 검증한 후 피격 및 디버프를 전달합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerBullet : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Attributes")]
    [Tooltip("탄환 기본 비행 속도")]
    [SerializeField] private float bulletSpeed = 6f;

    [Tooltip("탄환 피해량")]
    [SerializeField] private float bulletDamage = 13f;

    [Tooltip("탄환 자연 소멸 수명(초)")]
    [SerializeField] private float lifeTime = 2f;

    [Header("Slow Effect")]
    [Tooltip("감속 무기일 때 깎을 이동속도 비율 (0.5 = 50% 감속)")]
    [SerializeField] private float slowRatio = 0.5f;

    [Tooltip("감속 지속 시간(초)")]
    [SerializeField] private float slowDuration = 2.5f;

    [Header("Targeting")]
    [Tooltip("피격 대상 태그")]
    [SerializeField] private string targetTag = "Enemy";

    private WeaponType currentWeaponType = WeaponType.DefaultShotgun;
    private bool isDestroyed = false;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 발사 주체(PlayerAttack)로부터 탄환 속성을 주입받아 초기화
    /// </summary>
    public void InitBullet(WeaponType weaponType, Color bulletColor, float damage, Vector2 dir, Vector2 playerSpeed)
    {
        currentWeaponType = weaponType;
        bulletDamage = damage;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = bulletColor;
        }

        SetDirection(dir, playerSpeed);
    }

    private void SetDirection(Vector2 dir, Vector2 playerSpeed)
    {
        Vector2 normalizedDir = dir.normalized;
        float totalSpeed = bulletSpeed + playerSpeed.magnitude;
        rb.linearVelocity = normalizedDir * totalSpeed;

        float angle = Mathf.Atan2(normalizedDir.y, normalizedDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ProcessHit(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ProcessHit(other.gameObject);
    }

    private void ProcessHit(GameObject target)
    {
        if (isDestroyed || target == null) return;
        if (target.CompareTag("Player") || target.CompareTag("Bullet")) return;

        // 1. 규격에 따라 "Enemy" 태그를 가진 대상인지 1차 필터링 (자식/부모 콜라이더 대응)
        bool isEnemy = target.CompareTag(targetTag) || target.transform.root.CompareTag(targetTag);

        if (isEnemy)
        {
            Monster monster = target.GetComponentInParent<Monster>();
            if (monster != null)
            {
                // 피해 적용
                monster.TakeDamage(bulletDamage);

                // 감속 무기인 경우 동적 디버프 컴포넌트 부착
                if (currentWeaponType == WeaponType.CryoBlaster)
                {
                    MonsterSlowDebuff debuff = monster.gameObject.GetComponent<MonsterSlowDebuff>();
                    if (debuff == null)
                    {
                        debuff = monster.gameObject.AddComponent<MonsterSlowDebuff>();
                    }
                    debuff.ApplySlow(monster, slowRatio, slowDuration);
                }
            }

            isDestroyed = true;
            Destroy(gameObject);
            return;
        }

        // 몬스터가 아닌 장애물이나 벽에 부딪힌 경우 소멸
        isDestroyed = true;
        Destroy(gameObject);
    }
}
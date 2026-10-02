using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어의 사격 입력 수신, 무기 모드(Tab) 교체, 산탄 분산 궤적 계산 및 탄환 발사 전담 컴포넌트
/// 
/// [채택 이유 및 대안 비교]
/// 1. PlayerInputHandler 단일 의존:
///    - 대안: Update 내부에서 Input.GetKeyDown(KeyCode.Tab) 직접 수집.
///    - 채택 이유: 키 바인딩과 입력 레이어는 오직 PlayerInputHandler가 담당해야 합니다.
///      WasWeaponSwitchPressed 및 IsAttackPressed 상태만 소비하여 입력 체계 변경 시에도
///      공격 컴포넌트의 수정 없이 유연하게 확장 가능합니다.
/// 
/// 2. 산탄 펠릿 분산 및 무기 모드 캡슐화:
///    - Tab 키를 통해 기본 산탄(고화력)과 냉각탄(감속 유틸)의 색상/대미지/연사력을 
///      독립적으로 전환하며, 발사 지연 코루틴으로 공격 연사를 안정적으로 제어합니다.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Bullet Prefab")]
    [Tooltip("발사할 총알 프리팹 (PlayerBullet 스크립트 부착 필수)")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("총알이 생성될 총구 위치")]
    [SerializeField] private Transform bulletSpawnPoint;

    [Header("Weapon Modes")]
    [Tooltip("현재 선택된 무기 모드")]
    [SerializeField] private WeaponType currentWeapon = WeaponType.DefaultShotgun;

    [Header("Weapon 1 Settings (기본 산탄)")]
    [SerializeField] private Color weapon1Color = new Color(1f, 0.5f, 0.1f, 1f); // 주황색
    [SerializeField] private float weapon1Damage = 13f;
    [SerializeField] private float weapon1FireRate = 0.2f;

    [Header("Weapon 2 Settings (슬로우 냉각탄)")]
    [SerializeField] private Color weapon2Color = new Color(0.2f, 0.8f, 1f, 1f); // 하늘색
    [SerializeField] private float weapon2Damage = 8f;
    [SerializeField] private float weapon2FireRate = 0.25f;

    [Header("Shotgun Spread Attributes")]
    [Tooltip("한 번 사격 시 발사되는 펠릿(탄환) 개수")]
    [SerializeField] private int pelletCount = 5;

    [Tooltip("탄환 확산 총 각도")]
    [SerializeField] private float spreadAngle = 25f;

    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private Rigidbody2D playerRb;

    private bool canAttack = true;

    private void Reset()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        playerRb = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
        if (playerRb == null) playerRb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (inputHandler == null) return;

        // 1. Tab 키 입력 시 무기 교체 (PlayerInputHandler 단일 의존)
        if (inputHandler.WasWeaponSwitchPressed)
        {
            SwitchWeapon();
        }

        // 2. 인게임 상태 가드 검사 (대화, 일시정지, 웨이브 대기 중 사격 차단)
        if (!CanAttackInCurrentState()) return;

        // 3. Space 연사 공격
        if (inputHandler.IsAttackPressed && canAttack)
        {
            FireCurrentWeapon();
        }
    }

    /// <summary>
    /// Tab 키 입력 시 1번 무기와 2번 무기 순환 교체
    /// </summary>
    private void SwitchWeapon()
    {
        currentWeapon = (currentWeapon == WeaponType.DefaultShotgun)
            ? WeaponType.CryoBlaster
            : WeaponType.DefaultShotgun;

        Debug.Log($"[PlayerAttack] 무기 교체 완료 -> 현재 무기: {currentWeapon}");
    }

    private bool CanAttackInCurrentState()
    {
        if (GeneralManager.Instance?.inGameManager == null) return true;

        var igm = GeneralManager.Instance.inGameManager;
        if (igm.isTalking || igm.pauseVisible || !igm.isWave) return false;

        return true;
    }

    private void FireCurrentWeapon()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[PlayerAttack] Bullet Prefab이 할당되지 않았습니다.");
            return;
        }

        canAttack = false;

        Vector2 aimDir = inputHandler.AimDirection;
        Vector2 playerVel = playerRb != null ? playerRb.linearVelocity : Vector2.zero;
        Vector3 spawnPos = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;

        // 현재 무기 모드에 따른 속성 선택
        Color currentColor = (currentWeapon == WeaponType.DefaultShotgun) ? weapon1Color : weapon2Color;
        float currentDamage = (currentWeapon == WeaponType.DefaultShotgun) ? weapon1Damage : weapon2Damage;
        float currentFireRate = (currentWeapon == WeaponType.DefaultShotgun) ? weapon1FireRate : weapon2FireRate;

        // 산탄 확산 발사
        float startAngle = -spreadAngle * 0.5f;
        float angleStep = pelletCount > 1 ? spreadAngle / (pelletCount - 1) : 0f;

        for (int i = 0; i < pelletCount; i++)
        {
            float currentSpread = startAngle + (angleStep * i);
            Vector2 spreadDir = Quaternion.Euler(0f, 0f, currentSpread) * aimDir;

            GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            PlayerBullet bullet = bulletObj.GetComponent<PlayerBullet>();
            if (bullet != null)
            {
                bullet.InitBullet(currentWeapon, currentColor, currentDamage, spreadDir, playerVel);
            }
        }

        StartCoroutine(AttackCooldownRoutine(currentFireRate));
    }

    private IEnumerator AttackCooldownRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        canAttack = true;
    }
}
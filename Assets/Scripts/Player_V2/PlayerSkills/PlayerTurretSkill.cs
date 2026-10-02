using System.Collections;
using UnityEngine;

/// <summary>
/// Q키 입력 시 지정된 위치에 임시 보조 포탑을 소환하고 스킬 쿨타임을 제어하는 컴포넌트
/// 
/// [채택 이유 및 대안 비교]
/// 1. PlayerAttack / PlayerBomb 과의 완전 분리 (SRP 준수):
///    - 대안: 기존 PlayerAttack이나 PlayerBomb 내부에 터렛 소환 로직을 추가.
///    - 채택 이유: 신규 스킬 추가/삭제 시 다른 사격/폭탄 시스템에 영향을 주지 않도록
///      독립적인 컴포넌트로 격리하여 유지보수성을 극대화했습니다.
/// 
/// 2. 인게임 상태(InGameManager) 가드 일치:
///    - 포탑 준비 시간(!isWave), 대화 중, 일시정지 중에는 스킬 시전을 완벽하게 차단합니다.
/// </summary>
public class PlayerTurretSkill : MonoBehaviour
{
    [Header("Turret Prefab")]
    [Tooltip("소환할 보조 포탑 프리팹 (TemporaryTurret 컴포넌트 필수)")]
    [SerializeField] private GameObject turretPrefab;

    [Header("Skill Settings")]
    [Tooltip("포탑 스킬 쿨타임(초)")]
    [SerializeField] private float skillCooldown = 15f;

    [Tooltip("포탑 설치 위치 오프셋 (기본은 플레이어 발밑 위치)")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.5f, 0f);

    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;

    private bool isCooldown = false;
    private float currentCooldownTimer = 0f;

    public float CurrentCooldownTimer => currentCooldownTimer;
    public float SkillCooldown => skillCooldown;
    public bool IsCooldown => isCooldown;

    private void Reset()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Awake()
    {
        if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
        if (inputHandler == null) return;

        // 쿨타임 타이머 누적
        if (isCooldown)
        {
            currentCooldownTimer -= Time.deltaTime;
            if (currentCooldownTimer <= 0f)
            {
                isCooldown = false;
                currentCooldownTimer = 0f;
            }
        }

        // 인게임 상태 검사
        if (!CanUseSkillInCurrentState()) return;

        // Q키 입력 시 포탑 설치
        if (inputHandler.WasSkillQPressed && !isCooldown)
        {
            DeployTurret();
        }
    }

    private bool CanUseSkillInCurrentState()
    {
        if (GeneralManager.Instance?.inGameManager == null) return true;

        var igm = GeneralManager.Instance.inGameManager;
        if (igm.isTalking || igm.pauseVisible || !igm.isWave) return false;

        return true;
    }

    private void DeployTurret()
    {
        if (turretPrefab == null)
        {
            Debug.LogWarning("[PlayerTurretSkill] 포탑 프리팹이 할당되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = transform.position + spawnOffset;
        Instantiate(turretPrefab, spawnPos, Quaternion.identity);

        isCooldown = true;
        currentCooldownTimer = skillCooldown;
        Debug.Log($"[PlayerTurretSkill] 보조 포탑 소환 완료! (쿨타임 {skillCooldown}초)");
    }
}
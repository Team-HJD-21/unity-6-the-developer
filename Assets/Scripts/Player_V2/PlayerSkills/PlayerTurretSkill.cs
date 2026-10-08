using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택된 Q/E 슬롯에서 임시 보조 포탑을 소환하고 스킬 쿨타임을 제어하는 컴포넌트
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
    [SerializeField] private float skillCooldown = 3f;

    [SerializeField, Min(1)] private int maxTurretCount = 3;
    [SerializeField, Min(.1f)] private float recoveryRange = 2.5f;

    [Tooltip("포탑 설치 위치 오프셋 (기본은 플레이어 발밑 위치)")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.5f, 0f);

    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;

    private bool isCooldown = false;
    private float currentCooldownTimer = 0f;
    private PlayerSkillController _skills;
    private readonly HashSet<TemporaryTurret> _deployed = new HashSet<TemporaryTurret>();

    public float CurrentCooldownTimer => currentCooldownTimer;
    public float SkillCooldown => skillCooldown;
    public bool IsCooldown => isCooldown;
    public bool HasPrefab => turretPrefab != null && turretPrefab.GetComponent<TemporaryTurret>() != null;
    public int MaxTurretCount => maxTurretCount;
    public int CurrentTurretCount
    {
        get
        {
            _deployed.RemoveWhere(turret => turret == null);
            return Mathf.Max(0, maxTurretCount - _deployed.Count);
        }
    }
    public TemporaryTurret RecoverableTurret => FindRecoverableTurret();

    private void Reset()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Awake()
    {
        if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
        _skills = GetComponent<PlayerSkillController>();
    }

    private void Update()
    {
        TickCooldown(Time.deltaTime);
        if (inputHandler == null) return;

        // 슬롯이 있으면 입력은 Controller가 처리합니다.
        if (_skills != null) return;
        if (!CanUseSkillInCurrentState()) return;

        if (inputHandler.WasRecoverPressed) TryRecoverNearest();
        if (inputHandler.WasSkillQPressed) TryUseSkill();
    }

    private void TickCooldown(float elapsed)
    {
        if (isCooldown)
        {
            currentCooldownTimer -= Mathf.Max(0f, elapsed);
            if (currentCooldownTimer <= 0f)
            {
                isCooldown = false;
                currentCooldownTimer = 0f;
            }
        }
    }

    private bool CanUseSkillInCurrentState()
    {
        if (!GeneralManager.TryGetExistingInstance(out var manager) || manager.inGameManager == null) return true;

        var igm = manager.inGameManager;
        if (igm.isTalking || igm.pauseVisible || !igm.isWave) return false;

        return true;
    }

    private bool DeployTurret()
    {
        if (!HasPrefab || CurrentTurretCount <= 0) return false;

        Vector3 spawnPos = transform.position + spawnOffset;
        var turret = Instantiate(turretPrefab, spawnPos, Quaternion.identity).GetComponent<TemporaryTurret>();
        turret.AssignOwner(this);
        _deployed.Add(turret);

        isCooldown = true;
        currentCooldownTimer = skillCooldown;
        return true;
    }

    public bool TryUseSkill()
    {
        if (!isActiveAndEnabled || !HasPrefab || isCooldown || CurrentTurretCount <= 0) return false;
        if (_skills != null && (!_skills.CanUse || !_skills.Loadout.Contains(PlayerSkillId.TemporaryTurret))) return false;
        if (_skills == null && !CanUseSkillInCurrentState()) return false;
        return DeployTurret();
    }

    public bool TryRecoverNearest()
    {
        if (!isActiveAndEnabled) return false;
        if (_skills != null ? !_skills.CanRecover : !CanUseSkillInCurrentState()) return false;
        var turret = FindRecoverableTurret();
        return turret != null && turret.TryRecover(this);
    }

    private TemporaryTurret FindRecoverableTurret()
    {
        if (!isActiveAndEnabled || (_skills != null && !_skills.CanRecover)) return null;
        float closestDistance = recoveryRange * recoveryRange;
        TemporaryTurret nearest = null;
        foreach (var turret in _deployed)
        {
            if (turret == null || !turret.isActiveAndEnabled) continue;
            float distance = (turret.transform.position - transform.position).sqrMagnitude;
            if (distance > closestDistance) continue;
            closestDistance = distance;
            nearest = turret;
        }
        return nearest;
    }

    internal bool ReleaseTurret(TemporaryTurret turret) => _deployed.Remove(turret);

    private void OnDestroy()
    {
        foreach (var turret in _deployed)
        {
            if (turret == null) continue;
            turret.AssignOwner(null);
            if (Application.isPlaying) Destroy(turret.gameObject);
            else DestroyImmediate(turret.gameObject);
        }
        _deployed.Clear();
    }
}

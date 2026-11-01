// 플레이어의 체력, 피해, 회복과 사망 통지를 관리합니다.
// 초기 체력은 Prefab 설정을 사용하며 구형 성장 Manager에는 의존하지 않습니다.
using UnityEngine;
using UnityEngine.Events;

public partial class PlayerInfo : MonoBehaviour
{
    public int curHp;
    public int maxHp = 400;
    
    public UnityEvent onDeath = new UnityEvent();
    public UnityEvent<int,int> onHpChange = new UnityEvent<int, int>();
    private bool _isDead;
    private const double RecoveryInterval = 0.02;
    private bool _isRecovering;
    private double _nextRecoveryAt;
    public bool IsDead => _isDead;
    public bool IsRecovering => _isRecovering;

    private void Awake()
    {
        // Legacy 제거로 인해 로직 무효화: DataManager 기반 성장 체력 조회.
        // maxHp = DataManager.GetAttributeData(AttributeType.PlayerHealth);
        if (maxHp <= 0) maxHp = 400;
        curHp = maxHp;
    }

    private void FixedUpdate()
    {
        onHpChange.Invoke(curHp, maxHp);

        if (curHp <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (_isDead) return;
        _isDead = true;
        _isRecovering = false;
        GetComponent<PlayerControlState>()?.SetLifeBlocked(true);
        GetComponent<PlayerMovement>()?.StopImmediately();
        GetComponent<PlayerSkillController>()?.CloseSelection();
        onDeath.Invoke();
    }

    public void TakeDamage(int damage)
    {
        if (_isDead || damage <= 0) return;
        curHp = Mathf.Max(0, curHp - damage);
        
        if (curHp <= 0)
        {
            Die();
        }
    }

    public void RecoverHp()
    {
        if (_isDead || _isRecovering || !isActiveAndEnabled || curHp >= maxHp) return;
        _isRecovering = true;
        AdvanceRecovery();
    }

    private void Update()
    {
        // 내부 정비는 필드 정지와 구분하고 기존처럼 scaled Unity 게임 시간을 따릅니다.
        if (_isRecovering && !_isDead && Time.timeScale > 0 && Time.timeAsDouble >= _nextRecoveryAt) AdvanceRecovery();
    }

    private void AdvanceRecovery()
    {
        if (curHp >= maxHp) { curHp = maxHp; _isRecovering = false; return; }
        curHp = Mathf.Min(maxHp, curHp + 1);
        _isRecovering = curHp < maxHp;
        _nextRecoveryAt = Time.timeAsDouble + RecoveryInterval;
        onHpChange.Invoke(curHp, maxHp);
    }
}

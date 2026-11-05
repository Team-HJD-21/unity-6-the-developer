// 플레이어의 체력, 피해, 회복과 사망 통지를 관리합니다.
// 초기 체력은 Prefab 설정을 사용하며 구형 성장 Manager에는 의존하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets.Contracts;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.Events;

public partial class PlayerInfo : MonoBehaviour, IDamageableTarget, IPlayerMatchBinding, IPlayerNetworkMatchBinding
{
    public int curHp;
    public int maxHp = 400;
    
    public UnityEvent onDeath = new UnityEvent();
    public UnityEvent<int,int> onHpChange = new UnityEvent<int, int>();
    private bool _isDead;
    private const double RecoveryInterval = 0.02;
    private bool _recovering;
    private double _nextRecoveryAt;
    private bool _matchBound;
    private bool _healthAuthority;
    public int CurrentHealth => Mathf.Max(0, curHp);
    public int MaxHealth => maxHp;
    public bool IsDestroyed => _isDead || curHp <= 0;
    private bool CanWriteHealth => !_matchBound || _healthAuthority;

    private void Awake()
    {
        // Legacy 제거로 인해 로직 무효화: DataManager 기반 성장 체력 조회.
        // maxHp = DataManager.GetAttributeData(AttributeType.PlayerHealth);
        if (maxHp <= 0) maxHp = 400;
        curHp = maxHp;
    }

    private void FixedUpdate()
    {
        AdvanceRecovery();
        onHpChange.Invoke(curHp, maxHp);
        if (curHp <= 0) Die();
    }

    private void AdvanceRecovery()
    {
        if (CanWriteHealth && _recovering && !_isDead && FieldSimulationTime.CanAdvance(this) &&
            FieldSimulationTime.Seconds(this) >= _nextRecoveryAt)
        {
            curHp = Mathf.Min(maxHp, curHp + 1);
            _recovering = curHp < maxHp;
            _nextRecoveryAt = FieldSimulationTime.Seconds(this) + RecoveryInterval;
        }

    }

    public void Die()
    {
        if (_isDead) return;
        _isDead = true;
        _recovering = false;
        GetComponent<PlayerControlState>()?.SetLifeBlocked(true);
        GetComponent<PlayerMovement>()?.StopImmediately();
        GetComponent<PlayerSkillController>()?.CloseSelection();
        onDeath.Invoke();
    }

    public void TakeDamage(int damage)
    {
        ApplyDamage(damage);
    }

    /// <summary>서버 전투가 터렛과 동일한 피해 계약으로 호출합니다. Client 원본 변경은 거부합니다.</summary>
    public bool ApplyDamage(int damage)
    {
        if (!CanWriteHealth || _isDead || damage <= 0 || !FieldSimulationTime.CanAdvance(this)) return false;
        curHp = (int)Math.Max(0L, (long)curHp - damage);
        if (curHp <= 0) Die();
        return true;
    }

    public void RecoverHp()
    {
        if (!CanWriteHealth || _isDead || _recovering || curHp >= maxHp) return;
        _recovering = true;
        _nextRecoveryAt = FieldSimulationTime.Seconds(this);
        AdvanceRecovery();
    }

    public void BindMatch(MatchSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        _matchBound = true;
        _healthAuthority = true;
    }

    public void BindNetworkMatch(NetworkMatchCoordinator match, PlayerId localPlayerId)
    {
        if (match == null || localPlayerId.IsEmpty) throw new ArgumentException("A prepared Player binding is required.");
        _matchBound = true;
        _healthAuthority = false;
    }

    public void UnbindMatch() { _healthAuthority = false; _recovering = false; }
    public void UnbindNetworkMatch() => UnbindMatch();
}

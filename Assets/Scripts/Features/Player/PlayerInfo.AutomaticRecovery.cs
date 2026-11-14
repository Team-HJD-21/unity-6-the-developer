using System;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public partial class PlayerInfo
{
    [Header("Automatic health recovery")]
    [SerializeField, Min(0)] private float _automaticRecoveryDelay = 5f;
    [SerializeField, Range(0, 1)] private float _automaticRecoveryFractionPerSecond = .02f;
    private bool _automaticRecoveryPending;
    private double _automaticRecoverySampleAt, _automaticRecoveryPartialHealth;
    public float AutomaticRecoveryDelay => Mathf.Max(0, _automaticRecoveryDelay);
    public float AutomaticRecoveryFractionPerSecond => Mathf.Clamp01(_automaticRecoveryFractionPerSecond);

    private void ScheduleAutomaticRecovery()
    {
        ClearAutomaticRecovery();
        if (curHp <= 0 || curHp >= maxHp || !float.IsFinite(_automaticRecoveryDelay) ||
            !float.IsFinite(_automaticRecoveryFractionPerSecond) || AutomaticRecoveryFractionPerSecond <= 0) return;
        _automaticRecoveryPending = true;
        // Match 절대 시각을 사용하므로 배속을 중복 적용하거나 FixedUpdate 횟수로 회복 속도를 정하지 않습니다.
        _automaticRecoverySampleAt = FieldSimulationTime.Seconds(this) + AutomaticRecoveryDelay;
    }

    private void ClearAutomaticRecovery()
    {
        _automaticRecoveryPending = false;
        _automaticRecoverySampleAt = _automaticRecoveryPartialHealth = 0;
    }

    private void AdvanceAutomaticRecovery()
    {
        if (!isActiveAndEnabled || !CanWriteHealth || _isDead || !_automaticRecoveryPending || !FieldSimulationTime.CanAdvance(this)) return;
        if (curHp <= 0 || curHp >= maxHp) { ClearAutomaticRecovery(); return; }
        double now = FieldSimulationTime.Seconds(this);
        if (!double.IsFinite(now) || now < _automaticRecoverySampleAt) return;
        // 명시적 RecoverHp()의 기존 회복과 자동 회복이 중복되지 않습니다.
        if (_recovering) { _automaticRecoverySampleAt = now; return; }
        double elapsed = now - _automaticRecoverySampleAt;
        _automaticRecoverySampleAt = now;
        double earned = _automaticRecoveryPartialHealth + elapsed * maxHp * AutomaticRecoveryFractionPerSecond;
        int healed = (int)Math.Min(maxHp - curHp, Math.Floor(earned + .000001));
        curHp += healed;
        _automaticRecoveryPartialHealth = Math.Max(0, earned - healed);
        if (curHp >= maxHp) ClearAutomaticRecovery();
        // FixedUpdate의 기존 onHpChange가 HUD를 갱신합니다. Damaged/onDeath는 재발행하지 않습니다.
    }
}

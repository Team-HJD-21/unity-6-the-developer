using System;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Serialization;

public partial class PlayerInfo
{
    [Header("Automatic health recovery")]
    [SerializeField, Min(0)] private float _automaticRecoveryDelay = 5f;
    [FormerlySerializedAs("_automaticRecoveryFractionPerSecond")]
    [SerializeField, Range(0, 1)] private float _automaticRecoveryFractionPerPulse = .02f;
    [SerializeField, Min(.1f)] private float _automaticRecoveryInterval = 3f;
    private bool _automaticRecoveryPending;
    private double _automaticRecoverySampleAt, _automaticRecoveryPartialHealth;
    public float AutomaticRecoveryDelay => Mathf.Max(0, _automaticRecoveryDelay);
    public float AutomaticRecoveryFractionPerPulse => Mathf.Clamp01(_automaticRecoveryFractionPerPulse);
    public float AutomaticRecoveryInterval => Mathf.Max(.1f, _automaticRecoveryInterval);

    private void ScheduleAutomaticRecovery()
    {
        ClearAutomaticRecovery();
        if (curHp <= 0 || curHp >= maxHp || !float.IsFinite(_automaticRecoveryDelay) ||
            !float.IsFinite(_automaticRecoveryFractionPerPulse) || !float.IsFinite(_automaticRecoveryInterval) ||
            AutomaticRecoveryFractionPerPulse <= 0) return;
        _automaticRecoveryPending = true;
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
        // 놓친 횟수를 몰아서 지급하지 않고 실제 처리 시점에 한 번만 회복합니다.
        _automaticRecoverySampleAt = now + AutomaticRecoveryInterval;
        if (_recovering) return;
        double earned = _automaticRecoveryPartialHealth + maxHp * AutomaticRecoveryFractionPerPulse;
        int healed = (int)Math.Min(maxHp - curHp, Math.Floor(earned + .000001));
        curHp += healed;
        _automaticRecoveryPartialHealth = Math.Max(0, earned - Math.Floor(earned + .000001));
        if (curHp >= maxHp) ClearAutomaticRecovery();
        // FixedUpdate의 기존 onHpChange가 회복된 HP를 HUD에 전달합니다.
    }
}

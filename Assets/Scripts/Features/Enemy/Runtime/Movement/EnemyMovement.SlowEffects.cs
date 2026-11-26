using System;
using System.Collections.Generic;
using TeamHJD.Game.Enemy.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>외부 공격이 확정한 감속을 받아 실제 이동속도에 적용한다.</summary>
public partial class EnemyMovement : IAttackSlowEffectReceiver
{
    internal const int MaximumSlowEffects = 64;
    private List<SlowEffect> _slowEffects;
    private EnemyHealth _slowHealth;
    private EnemyNetworkController _slowNetwork;

    /// <summary>활성 효과 중 가장 작은 속도 배율. 효과가 없으면 1이다.</summary>
    public float SlowSpeedMultiplier
    {
        get
        {
            if (_slowEffects == null || _slowEffects.Count == 0) return 1f;
            double now = FieldSimulationTime.Seconds(this);
            float multiplier = 1f;
            for (int i = _slowEffects.Count - 1; i >= 0; i--)
            {
                SlowEffect effect = _slowEffects[i];
                if (effect.EndsAt <= now)
                    _slowEffects.RemoveAt(i);
                else
                    multiplier = Mathf.Min(multiplier, effect.Multiplier);
            }
            return multiplier;
        }
    }

    /// <summary>공격자 정보가 없는 공통 감속 계약이다. 공격 규칙이 있는 대상에는 TryApplySlow로 출처를 전달한다.</summary>
    public void ApplySlow(float speedMultiplier, float duration)
        => TryApplySlow(speedMultiplier, duration, AttackSourceType.Unknown);

    /// <summary>
    /// 권한과 피격 규칙을 통과한 생존 적에게 감속을 적용한다.
    /// 같은 배율은 종료 시간을 갱신하고, 서로 다른 배율은 독립적으로 만료한다.
    /// </summary>
    /// <param name="speedMultiplier">0 초과 1 미만의 속도 배율. 0.5는 50% 감속이다.</param>
    /// <param name="duration">양수인 Match 게임 초. 정지 중에는 시간이 흐르지 않는다.</param>
    /// <param name="source">확정한 공격의 출처. 냉각 스프레이는 Player를 전달한다.</param>
    /// <returns>새 효과가 적용되거나 기존 효과의 기한을 갱신했으면 true.</returns>
    public bool TryApplySlow(float speedMultiplier, float duration, AttackSourceType source)
    {
        if (!float.IsFinite(speedMultiplier) || speedMultiplier <= 0f || speedMultiplier >= 1f ||
            !float.IsFinite(duration) || duration <= 0f || !isActiveAndEnabled ||
            !FieldSimulationTime.CanAdvance(this))
            return false;

        if (_slowNetwork == null) TryGetComponent(out _slowNetwork);
        if (_slowNetwork != null && _slowNetwork.IsSpawned && !_slowNetwork.IsServer)
            return false;
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager != null && manager.IsListening &&
            (_slowNetwork == null || !_slowNetwork.IsSpawned || !_slowNetwork.IsServer))
            return false;
        if (_slowHealth == null) TryGetComponent(out _slowHealth);
        if (_slowHealth == null || !_slowHealth.CanBeAttackedBy(source))
            return false;

        double now = FieldSimulationTime.Seconds(this);
        double endsAt = now + duration;
        if (!double.IsFinite(now) || !double.IsFinite(endsAt)) return false;
        _slowEffects ??= new List<SlowEffect>();
        for (int i = _slowEffects.Count - 1; i >= 0; i--)
        {
            SlowEffect effect = _slowEffects[i];
            if (effect.EndsAt <= now)
                _slowEffects.RemoveAt(i);
            else if (effect.Multiplier == speedMultiplier)
            {
                _slowEffects[i] = new SlowEffect(speedMultiplier, Math.Max(effect.EndsAt, endsAt));
                return true;
            }
        }
        if (_slowEffects.Count >= MaximumSlowEffects) return false;
        _slowEffects.Add(new SlowEffect(speedMultiplier, endsAt));
        return true;
    }

    private void OnDisable()
    {
        DisableAgentMovement();
        _slowEffects?.Clear();
    }

    private readonly struct SlowEffect
    {
        public readonly float Multiplier;
        public readonly double EndsAt;
        public SlowEffect(float multiplier, double endsAt)
        { Multiplier = multiplier; EndsAt = endsAt; }
    }
}

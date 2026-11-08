// 적이 확정한 Player 감속을 수신하고 공용 게임 시각으로 각 효과를 만료합니다.
// 중첩은 살아 있는 효과 중 가장 강한 하나를 적용하며 각 효과의 기한은 독립적입니다.
// 실제 이동 원본은 PlayerMovement이며 이 컴포넌트는 배율과 효과 상태만 소유합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Enemy.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class PlayerSlowEffects : MonoBehaviour, ISlowEffectReceiver
{
    internal const int MaximumEffects = 4096;
    private readonly List<SlowEffect> _effects = new();
    private float _publishedMultiplier = 1;
    private int _publishedCount;
    private double GameSeconds => FieldSimulationTime.Seconds(this);

    /// <summary>이동·Dash의 최종 속도에 적용하는 현재 배율입니다. 입력/생존 잠금은 변경하지 않습니다.</summary>
    public float SpeedMultiplier
    {
        get
        {
            double now = GameSeconds;
            float multiplier = 1;
            foreach (var effect in _effects)
                if (effect.EndsAt > now)
                    multiplier = Mathf.Min(multiplier, effect.Multiplier);
            return multiplier;
        }
    }

    public int ActiveEffectCount
    {
        get
        {
            double now = GameSeconds;
            int count = 0;
            foreach (var effect in _effects)
                if (effect.EndsAt > now)
                    count++;
            return count;
        }
    }

    /// <summary>HUD/효과 View가 자신의 수명에서 구독합니다. 배율 또는 활성 효과 수가 바뀌면 통지합니다.</summary>
    public event Action<float, int> Changed;

    /// <summary>Host에서 적의 확정 피격 처리가 호출합니다. Client·사망·필드 정지/종료 중에는 적용하지 않습니다.</summary>
    public void ApplySlow(float speedMultiplier, float duration)
    {
        if (!float.IsFinite(speedMultiplier) || speedMultiplier <= 0 || speedMultiplier > 1)
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        if (!float.IsFinite(duration) || duration < 0)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (!isActiveAndEnabled || duration == 0 || speedMultiplier == 1 || !FieldSimulationTime.CanAdvance(this))
            return;
        var presence = GetComponent<PlayerMatchPresence>();
        if (presence?.TimeSource != null && (!presence.CanReport || !presence.CanSimulateField))
            return;
        if (GetComponent<PlayerInfo>()?.IsDestroyed == true)
            return;
        RemoveExpired();
        if (_effects.Count >= MaximumEffects)
            throw new InvalidOperationException("Player slow effects exceed the live-state limit.");
        _effects.Add(new SlowEffect(speedMultiplier, GameSeconds + duration));
        PublishChanged();
    }

    private void Update()
    {
        RemoveExpired();
        PublishChanged();
    }

    private void RemoveExpired()
    {
        double now = GameSeconds;
        for (int i = _effects.Count - 1; i >= 0; i--)
            if (_effects[i].EndsAt <= now)
                _effects.RemoveAt(i);
    }

    private void PublishChanged()
    {
        float multiplier = SpeedMultiplier;
        int count = ActiveEffectCount;
        if (multiplier == _publishedMultiplier && count == _publishedCount)
            return;
        _publishedMultiplier = multiplier;
        _publishedCount = count;
        if (Changed == null)
            return;
        foreach (Action<float, int> observer in Changed.GetInvocationList())
            try
            {
                observer(multiplier, count);
            }
            catch (Exception error) { Debug.LogException(error, this); }
    }

    private readonly struct SlowEffect
    {
        public readonly float Multiplier;
        public readonly double EndsAt;
        public SlowEffect(float multiplier, double endsAt)
        {
            Multiplier = multiplier;
            EndsAt = endsAt;
        }
    }
}

// 대시 진행·재사용 대기의 값과 남은 시간을 보관합니다.
using System;

/// <summary>대시 지속시간과 재사용 대기시간을 계산합니다. 입력·이동·잔상 표시는 관리하지 않습니다.</summary>
public sealed class PlayerDashTimer
{
    public float Remaining { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownDuration { get; private set; }
    public bool IsDashing => Remaining > 0;
    public bool IsReady => !IsDashing && CooldownRemaining <= 0;

    public bool TryStart(float duration, float cooldown)
    {
        if (!IsReady || !Finite(duration) || duration <= 0 || !Finite(cooldown) || cooldown < 0) return false;
        Remaining = duration;
        CooldownRemaining = CooldownDuration = cooldown;
        return true;
    }

    public void TickCooldown(float elapsed)
    {
        if (Finite(elapsed) && elapsed > 0) CooldownRemaining = Math.Max(0, CooldownRemaining - elapsed);
    }

    public float ConsumeStep(float elapsed)
    {
        if (!Finite(elapsed) || elapsed <= 0) return 0;
        float consumed = Math.Min(Remaining, elapsed);
        Remaining = Math.Max(0, Remaining - consumed);
        return consumed;
    }

    public void Cancel() => Remaining = 0;
    /// <summary>Player writer가 검증한 진행을 적용합니다. 대시 사용 요청이나 쿨다운 소비를 다시 실행하지 않습니다.</summary>
    internal void Restore(float remaining, float cooldownRemaining, float cooldownDuration)
    {
        if (!Finite(remaining) || remaining < 0 || !Finite(cooldownRemaining) || cooldownRemaining < 0 ||
            !Finite(cooldownDuration) || cooldownDuration < cooldownRemaining)
            throw new ArgumentOutOfRangeException(nameof(remaining));
        Remaining = remaining;
        CooldownRemaining = cooldownRemaining;
        CooldownDuration = cooldownDuration;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

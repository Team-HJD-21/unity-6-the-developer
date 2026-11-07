// 스킬 보유 수량과 순차 충전을 관리하고 소유 스킬의 저장·복원에 실제 진행 값을 제공합니다.
using System;
public sealed class PlayerSkillCharges
{
    private readonly int _maximum;
    private readonly float _rechargeSeconds;
    public int Current { get; private set; }
    public float Remaining { get; private set; }
    public PlayerSkillCharges(int maximum, float rechargeSeconds)
    {
        _maximum = Math.Max(1, maximum);
        _rechargeSeconds = float.IsNaN(rechargeSeconds) || float.IsInfinity(rechargeSeconds) ? 18 : Math.Max(.1f, rechargeSeconds);
        Current = _maximum;
    }
    public bool TryConsume()
    {
        if (Current <= 0) return false;
        if (Current == _maximum) Remaining = _rechargeSeconds;
        Current--;
        return true;
    }
    public void Tick(float elapsed)
    {
        if (Current >= _maximum || elapsed <= 0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
        Remaining -= elapsed;
        while (Remaining <= 0 && Current < _maximum) { Current++; Remaining += _rechargeSeconds; }
        if (Current == _maximum) Remaining = 0;
    }

    /// <summary>소유 스킬 writer가 현재 충전 수량·남은 첫 충전 시간과 정의를 복사합니다.</summary>
    public State CaptureState() => new(_maximum, _rechargeSeconds, Current, Remaining);

    /// <summary>소유 스킬의 사전 검증/복원 단계가 같은 충전 정의에 적용합니다. 새 충전을 시작하지 않습니다.</summary>
    public void RestoreState(State state)
    {
        Validate(state.Maximum, state.RechargeSeconds, state.Current, state.Remaining);
        if (state.Maximum != _maximum || state.RechargeSeconds != _rechargeSeconds)
            throw new ArgumentException("The saved charge definition does not match this skill.", nameof(state));
        Current = state.Current; Remaining = state.Remaining;
    }

    public readonly struct State
    {
        public int Maximum { get; }
        public float RechargeSeconds { get; }
        public int Current { get; }
        public float Remaining { get; }
        public State(int maximum, float rechargeSeconds, int current, float remaining)
        {
            Validate(maximum, rechargeSeconds, current, remaining);
            Maximum = maximum; RechargeSeconds = rechargeSeconds; Current = current; Remaining = remaining;
        }
    }

    private static void Validate(int maximum, float recharge, int current, float remaining)
    {
        if (maximum < 1 || float.IsNaN(recharge) || float.IsInfinity(recharge) || recharge < .1f ||
            current < 0 || current > maximum || float.IsNaN(remaining) || float.IsInfinity(remaining) ||
            remaining < 0 || remaining > recharge || (current == maximum && remaining != 0))
            throw new ArgumentException("Invalid saved skill charge state.");
    }
}

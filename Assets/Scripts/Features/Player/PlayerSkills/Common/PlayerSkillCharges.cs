using System;

/// <summary>스킬 보유 수량과 순차 충전을 관리합니다.</summary>
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
}

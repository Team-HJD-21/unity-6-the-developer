// 승인된 HUD의 등장/숨김 보간입니다. 논리 상태와 표시 전환을 분리하고 중간 반전을 현재 값에서 잇습니다.
using UnityEngine;

public static class CombatHudMotion
{
    public static float Sigmoid(float time, float slope = 10f)
    {
        time = Mathf.Clamp01(time);
        slope = Mathf.Max(.01f, slope);
        float start = 1f / (1f + Mathf.Exp(slope * .5f));
        float end = 1f / (1f + Mathf.Exp(-slope * .5f));
        return (1f / (1f + Mathf.Exp(-slope * (time - .5f))) - start) / (end - start);
    }
}

[System.Serializable]
public sealed class CombatHudTransition
{
    public float Value { get; private set; }
    public float Target { get; private set; }
    public bool IsMoving => _elapsed < _duration;
    private float _from, _elapsed = 1f, _duration;
    public void Snap(float value) { Value = Target = _from = value; _elapsed = _duration = 0; }
    public void Set(float target, float seconds)
    {
        if (Mathf.Approximately(Target, target)) return;
        _from = Value; Target = target; _elapsed = 0; _duration = Mathf.Max(.001f, seconds);
    }
    public float Tick(float elapsed)
    {
        _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0, elapsed));
        Value = Mathf.LerpUnclamped(_from, Target, _duration <= 0 ? 1 : CombatHudMotion.Sigmoid(_elapsed / _duration));
        return Value;
    }
}

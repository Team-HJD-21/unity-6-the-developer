using System;
using System.Collections.Generic;

/// <summary>설치된 포탑 수량과 손상 비율에 따른 회수·파괴 후 재충전을 관리합니다.</summary>
public sealed class PlayerTurretInventory
{
    private readonly int _capacity;
    private readonly float _maximumRepairSeconds;
    private readonly List<float> _repairs = new();
    public int Deployed { get; private set; }
    public int Available => Math.Max(0, _capacity - Deployed - _repairs.Count);
    public float RepairRemaining
    {
        get { float result = float.PositiveInfinity; foreach (float repair in _repairs) result = Math.Min(result, repair); return _repairs.Count > 0 ? result : 0; }
    }
    public float MaximumRepairSeconds => _maximumRepairSeconds;
    public PlayerTurretInventory(int capacity, float maximumRepairSeconds)
    {
        _capacity = Math.Max(1, capacity);
        _maximumRepairSeconds = float.IsNaN(maximumRepairSeconds) || float.IsInfinity(maximumRepairSeconds) ? 30 : Math.Max(0, maximumRepairSeconds);
    }
    public bool TryDeploy() { if (Available <= 0) return false; Deployed++; return true; }
    public bool Return(float healthRatio)
    {
        if (Deployed <= 0) return false;
        Deployed--;
        if (float.IsNaN(healthRatio) || float.IsInfinity(healthRatio)) healthRatio = 0;
        float repair = (1 - Math.Max(0, Math.Min(1, healthRatio))) * _maximumRepairSeconds;
        if (repair > 0) _repairs.Add(repair);
        return true;
    }
    public void Tick(float elapsed)
    {
        if (elapsed <= 0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
        for (int i = _repairs.Count - 1; i >= 0; i--) { _repairs[i] -= elapsed; if (_repairs[i] <= 0) _repairs.RemoveAt(i); }
    }
}

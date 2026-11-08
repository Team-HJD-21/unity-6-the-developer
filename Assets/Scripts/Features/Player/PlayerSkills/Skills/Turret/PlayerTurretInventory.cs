// 임시 포탑의 설치 수와 손상 회수 후 개별 재충전 시간을 소유합니다.
// 가장 짧은 남은 시간만 저장하지 않고 모든 수리 진행을 복사해 복원합니다.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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

    /// <summary>임시 포탑 스킬 writer가 실제 설치 수·모든 재충전 진행을 복사합니다. 배치 actor는 스킬이 별도 캡처합니다.</summary>
    public State CaptureState() => new(_capacity, _maximumRepairSeconds, Deployed, _repairs);
    /// <summary>소유 스킬이 같은 정의와 저장된 설치 actor 수를 검증한 뒤 적용합니다. 회수 사건을 다시 발생시키지 않습니다.</summary>
    public void RestoreState(State state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (state.Capacity != _capacity || state.MaximumRepairSeconds != _maximumRepairSeconds)
            throw new ArgumentException("The saved inventory definition does not match this skill.", nameof(state));
        _repairs.Clear(); _repairs.AddRange(state.Repairs); Deployed = state.Deployed;
    }

    public sealed class State
    {
        public int Capacity { get; }
        public float MaximumRepairSeconds { get; }
        public int Deployed { get; }
        public IReadOnlyList<float> Repairs { get; }
        public State(int capacity, float maximumRepairSeconds, int deployed, IEnumerable<float> repairs)
        {
            if (capacity < 1 || deployed < 0 || deployed > capacity || float.IsNaN(maximumRepairSeconds) ||
                float.IsInfinity(maximumRepairSeconds) || maximumRepairSeconds < 0)
                throw new ArgumentException("Invalid saved turret inventory definition.");
            if (repairs == null) throw new ArgumentNullException(nameof(repairs));
            var copy = new List<float>();
            foreach (float remaining in repairs)
            {
                if (copy.Count >= capacity - deployed || float.IsNaN(remaining) || float.IsInfinity(remaining) ||
                    remaining <= 0 || remaining > maximumRepairSeconds)
                    throw new ArgumentException("Invalid saved turret repair queue.");
                copy.Add(remaining);
            }
            Capacity = capacity; MaximumRepairSeconds = maximumRepairSeconds; Deployed = deployed;
            Repairs = new ReadOnlyCollection<float>(copy);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>한 판 안의 고정 포탑 슬롯과 회수 체력·파괴 후 복구 시간을 소유합니다.</summary>
public sealed class PlayerTurretInventory
{
    public enum SlotStatus { Available, Deployed, Rebuilding }
    public readonly struct Slot
    {
        public SlotStatus Status { get; }
        public float HealthRatio { get; }
        public float Remaining { get; }
        public Slot(SlotStatus status, float healthRatio, float remaining = 0)
        { Status = status; HealthRatio = healthRatio; Remaining = remaining; }
    }
    private readonly Slot[] _slots;
    public int Capacity => _slots.Length;
    public float MaximumRepairSeconds { get; }
    public int Available => Count(SlotStatus.Available);
    public int Deployed => Count(SlotStatus.Deployed);
    public float RepairRemaining
    {
        get
        {
            float result = float.PositiveInfinity;
            foreach (var slot in _slots) if (slot.Status == SlotStatus.Rebuilding) result = Math.Min(result, slot.Remaining);
            return float.IsPositiveInfinity(result) ? 0 : result;
        }
    }
    public PlayerTurretInventory(int capacity, float maximumRepairSeconds)
    {
        _slots = new Slot[Math.Max(1, capacity)];
        MaximumRepairSeconds = !float.IsFinite(maximumRepairSeconds) ? 30 : Math.Max(0, maximumRepairSeconds);
        for (int i = 0; i < _slots.Length; i++) _slots[i] = new Slot(SlotStatus.Available, 1);
    }
    public Slot GetSlot(int index) => _slots[index];
    public bool TryDeploy(int index)
    {
        if (index < 0 || index >= Capacity || _slots[index].Status != SlotStatus.Available) return false;
        _slots[index] = new Slot(SlotStatus.Deployed, _slots[index].HealthRatio);
        return true;
    }
    public bool TryDeploy()
    {
        for (int i = 0; i < Capacity; i++) if (TryDeploy(i)) return true;
        return false;
    }
    public bool UpdateHealth(int index, float healthRatio)
    {
        if (index < 0 || index >= Capacity || _slots[index].Status != SlotStatus.Deployed ||
            !float.IsFinite(healthRatio) || healthRatio <= 0 || healthRatio > 1) return false;
        _slots[index] = new Slot(SlotStatus.Deployed, healthRatio);
        return true;
    }
    public bool Return(int index, float healthRatio)
    {
        if (index < 0 || index >= Capacity || _slots[index].Status != SlotStatus.Deployed ||
            !float.IsFinite(healthRatio) || healthRatio < 0 || healthRatio > 1) return false;
        _slots[index] = healthRatio > 0 ? new Slot(SlotStatus.Available, healthRatio)
            : MaximumRepairSeconds > 0 ? new Slot(SlotStatus.Rebuilding, 0, MaximumRepairSeconds)
            : new Slot(SlotStatus.Available, 1);
        return true;
    }
    public bool Return(float healthRatio)
    {
        for (int i = 0; i < Capacity; i++) if (_slots[i].Status == SlotStatus.Deployed) return Return(i, healthRatio);
        return false;
    }
    public void Tick(float elapsed)
    {
        if (!float.IsFinite(elapsed) || elapsed <= 0) return;
        for (int i = 0; i < Capacity; i++)
        {
            if (_slots[i].Status != SlotStatus.Rebuilding) continue;
            float remaining = Math.Max(0, _slots[i].Remaining - elapsed);
            _slots[i] = remaining > 0 ? new Slot(SlotStatus.Rebuilding, 0, remaining) : new Slot(SlotStatus.Available, 1);
        }
    }
    private int Count(SlotStatus status)
    { int count = 0; foreach (var slot in _slots) if (slot.Status == status) count++; return count; }

    public State CaptureState() => new(Capacity, MaximumRepairSeconds, _slots);
    public void RestoreState(State state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (state.Capacity != Capacity || state.MaximumRepairSeconds != MaximumRepairSeconds)
            throw new ArgumentException("The saved inventory definition does not match this skill.", nameof(state));
        for (int i = 0; i < Capacity; i++) _slots[i] = state.Slots[i];
    }

    public sealed class State
    {
        public int Capacity { get; }
        public float MaximumRepairSeconds { get; }
        public int Deployed { get; }
        public IReadOnlyList<Slot> Slots { get; }
        public State(int capacity, float maximumRepairSeconds, IEnumerable<Slot> slots)
        {
            if (capacity < 1 || !float.IsFinite(maximumRepairSeconds) || maximumRepairSeconds < 0)
                throw new ArgumentException("Invalid saved turret inventory definition.");
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            var copy = new List<Slot>();
            foreach (var slot in slots)
            {
                if (copy.Count >= capacity || !Enum.IsDefined(typeof(SlotStatus), slot.Status) ||
                    !float.IsFinite(slot.HealthRatio) || !float.IsFinite(slot.Remaining) ||
                    slot.HealthRatio < 0 || slot.HealthRatio > 1 || slot.Remaining < 0 ||
                    (slot.Status == SlotStatus.Rebuilding
                        ? slot.HealthRatio != 0 || slot.Remaining <= 0 || slot.Remaining > maximumRepairSeconds
                        : slot.HealthRatio <= 0 || slot.Remaining != 0))
                    throw new ArgumentException("Invalid saved turret slot.");
                if (slot.Status == SlotStatus.Deployed) Deployed++;
                copy.Add(slot);
            }
            if (copy.Count != capacity) throw new ArgumentException("Missing saved turret slots.");
            Capacity = capacity; MaximumRepairSeconds = maximumRepairSeconds; Slots = new ReadOnlyCollection<Slot>(copy);
        }
    }
}

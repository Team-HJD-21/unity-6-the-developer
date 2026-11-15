// 완료 사실을 짧게 합칩니다. Alert 큐나 게임 점유 상태를 소유하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;

public sealed class TerritoryCaptureNotices
{
    private readonly Dictionary<int, double> _regions = new();
    private double _ready, _expires, _first;
    private long _revision;
    private RegionGridSnapshot _validated;
    public int Count => _regions.Count;
    public string Key(RegionGridSnapshot snapshot) => $"territory/{snapshot.MatchId}/{snapshot.GridId:N}/{_revision}";

    public void Observe(RegionGridSnapshot snapshot, TerritoryExpansionState.Receipt receipt, double now, double waveEnd, float merge, float fresh)
    {
        if (receipt == TerritoryExpansionState.Receipt.Baseline) { Clear(); return; }
        Prune(snapshot, now);
        if (receipt != TerritoryExpansionState.Receipt.Capture || snapshot.Change.Kind != RegionChangeKind.StaticSecured) return;
        bool empty = _regions.Count == 0;
        foreach (int id in snapshot.Change.SecuredRegionIds)
            if (IsCaptured(snapshot, id)) _regions[id] = now;
        if (_regions.Count == 0) return;
        if (empty) _first = now;
        _revision = snapshot.Change.Revision;
        _ready = Math.Max(now + Math.Max(0, merge), double.IsNegativeInfinity(waveEnd) ? now : waveEnd);
        _expires = Math.Min(_first + 2.5, _ready + Math.Max(0, fresh));
    }

    public void Align(double waveEnd, float fresh)
    {
        if (_regions.Count == 0 || waveEnd <= _ready || double.IsInfinity(waveEnd)) return;
        _ready = waveEnd; _expires = Math.Min(_first + 2.5, _ready + Math.Max(0, fresh));
    }
    public bool Ready(RegionGridSnapshot snapshot, double now)
    { Prune(snapshot, now); return _regions.Count > 0 && now >= _ready && now <= _expires; }
    public void Clear() { _regions.Clear(); _ready = _expires = _first = 0; _revision = -1; _validated = null; }

    private void Prune(RegionGridSnapshot snapshot, double now)
    {
        if (snapshot == null || (_regions.Count > 0 && now > _expires)) { Clear(); return; }
        if (ReferenceEquals(snapshot, _validated)) return;
        _validated = snapshot;
        // 매우 작은 확보 묶음이며 프레임마다 임시 목록을 만들지 않습니다.
        int remove;
        do
        {
            remove = -1;
            foreach (var item in _regions) if (!IsCaptured(snapshot, item.Key)) { remove = item.Key; break; }
            if (remove >= 0) _regions.Remove(remove);
        } while (remove >= 0);
    }
    public static bool IsCaptured(RegionGridSnapshot snapshot, int regionId)
    {
        if (snapshot == null) return false;
        bool found = false;
        for (int i = 0; i < snapshot.CapturedCells.Count; i++)
        {
            if (snapshot.Definition.RegionIds[i] != regionId || !snapshot.Definition.CapturableCells[i]) continue;
            found = true; if (!snapshot.CapturedCells[i]) return false;
        }
        return found;
    }
}

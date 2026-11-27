using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

public sealed partial class TacticalCommandController
{
    public readonly struct EnemyGroup
    {
        public readonly Vector2 Position;
        public readonly int Count;
        public EnemyGroup(Vector2 position, int count) { Position = position; Count = count; }
    }
    private readonly List<TurretPresentationSnapshot> _rows = new();
    private readonly Dictionary<EntityId, TurretPresentationSnapshot> _byId = new();
    private readonly Dictionary<EntityId, FieldTurretBase> _locals = new();
    private readonly Dictionary<FieldTurretBase, EntityId> _localIds = new();
    private readonly Dictionary<EntityId, TacticalPresentationRules.Vitality> _damage = new();
    private readonly List<Vector2> _enemies = new();
    private readonly List<EnemyGroup> _groups = new();
    private readonly Dictionary<Vector2Int, (Vector2 sum, int count)> _buckets = new();
    private EnemyHealth[] _enemySources = System.Array.Empty<EnemyHealth>();
    private float _nextEnemyDiscovery;
    public IReadOnlyList<EnemyGroup> EnemyGroups => _groups;
    public TurretPresentationSnapshot Selected => _byId.TryGetValue(_selected, out var row) ? row : null;
    public static Vector2 Point(TurretPresentationSnapshot row) => new((float)row.Position.X, (float)row.Position.Y);
    public bool IsSelected(EntityId id) => id == _selected || _batch.Contains(id);
    public bool WasHit(EntityId id) => _damage.TryGetValue(id, out var sample) && Time.unscaledTime - sample.HitAt < Style.HitHoldSeconds;
    public int EnemyCount(TurretPresentationSnapshot row)
    {
        if (row == null) return 0; int count = 0; float squared = (float)(row.Range * row.Range); Vector2 p = Point(row);
        foreach (var enemy in _enemies) if ((enemy - p).sqrMagnitude <= squared) count++;
        return count;
    }
    private bool CaptureData()
    {
        _rows.Clear();
        _byId.Clear();
        _locals.Clear();
        _localIds.Clear();
        Regions = null;
        Battlefield = null;
        _captureProgress.Clear();
        if (_player == null || Source.Turrets == null)
        {
            _enemies.Clear();
            _groups.Clear();
            return false;
        }

        bool useMatchFrame = Source.RequiresCommands;
        IndexLocalTurrets(useMatchFrame);
        var frame = Source.Commands?.Presentation.Snapshot;
        if (useMatchFrame && (frame == null || frame.MatchId != Source.BoundMatchId))
        {
            _enemies.Clear();
            _groups.Clear();
            _selected = default;
            _batch.Clear();
            ClearUnboundMapView();
            return false;
        }

        // Match 프레임은 생성할 때 이미 EntityId 순서로 정렬되며, 장면 직접 실행 때만 로컬 표시를 만듭니다.
        if (useMatchFrame) _rows.AddRange(frame.Turrets);
        else CaptureLocalTurretRows();

        float now = Time.unscaledTime;
        foreach (var row in _rows)
        {
            _byId[row.EntityId] = row;
            if (!_damage.TryGetValue(row.EntityId, out var sample))
                _damage[row.EntityId] = sample = new TacticalPresentationRules.Vitality();
            sample.Sample(row.CurrentHealth, row.MaxHealth, now);
        }
        foreach (var id in _damage.Keys.Where(id => !_byId.ContainsKey(id)).ToArray()) _damage.Remove(id);
        _batch.RemoveWhere(id => !_byId.ContainsKey(id));
        if (!_byId.ContainsKey(_selected))
        {
            _selected = default;
            foreach (var remaining in _batch) { _selected = remaining; break; }
        }

        var regions = Source.Turrets.Regions?.Snapshot;
        var controller = frame?.Controller;
        if (regions != null && (!useMatchFrame || (regions.MatchId == _match &&
            regions.GridId == controller.RegionGridId && regions.Revision >= controller.RegionRevision)))
            Regions = regions;
        _playerVitality.Sample(_player.CurrentHealth, _player.MaxHealth, now);
        if (useMatchFrame && controller.IsControlUnitAvailable)
            _cuVitality.Sample(controller.ControlUnitHealth, controller.ControlUnitMaxHealth, now);
        else if (!useMatchFrame && Source.ControlUnit != null)
            _cuVitality.Sample(Source.ControlUnit.CurrentHealth, Source.ControlUnit.MaxHealth, now);

        CaptureIntel();
        UpdateBounds();
        CaptureEnemies();
        RefreshIncidents();
        RefreshRegionRaster();
        return true;
    }

    private void IndexLocalTurrets(bool useMatchFrame)
    {
        foreach (var turret in FieldTurretInstanceRegistry.GetAll())
        {
            if (turret == null || !Source.Turrets.OwnsTurret(turret) || turret.Definition == null) continue;
            if (turret.TryGetEntityId(out var id)) _locals[id] = turret;
            else if (!useMatchFrame) _locals[new EntityId("local/" + turret.InstanceId)] = turret;
        }
        foreach (var local in _locals) _localIds[local.Value] = local.Key;
    }

    private void CaptureLocalTurretRows()
    {
        foreach (var item in _locals)
        {
            var t = item.Value;
            var flags = TurretPresentationFlags.None;
            if (t.IsInitialized) flags |= TurretPresentationFlags.Initialized;
            if (t.IsActivated) flags |= TurretPresentationFlags.Activated;
            if (t.IsOperational) flags |= TurretPresentationFlags.Operational;
            if (t.IsDestroyed) flags |= TurretPresentationFlags.Destroyed;
            if (t.IsLocked) flags |= TurretPresentationFlags.Locked;
            if (t.IsManualOperationBlocked) flags |= TurretPresentationFlags.ManualOperationBlocked;
            Vector2 p = t.transform.position;
            _rows.Add(new TurretPresentationSnapshot(item.Key, new DefinitionId(t.Definition.Id),
                string.IsNullOrWhiteSpace(t.DisplayName) ? t.Definition.Id : t.DisplayName, new BattlefieldPoint(p.x, p.y),
                t.Definition.Level, t.CurrentHealth, t.MaxHealth, t.EffectiveDamage, t.EffectivePower, t.EffectiveRange,
                t.CoolingDownRemaining, new DefinitionId(t.RuntimeState.SelectedUpgradeId), t.RuntimeState.UpgradeLevel, flags));
        }
        _rows.Sort((a, b) => string.CompareOrdinal(a.EntityId.Value, b.EntityId.Value));
    }
    private void UpdateBounds()
    {
        if (TryGetAuthoredCameraBounds(out Rect authored)) { WorldBounds = authored; return; }
        if (Regions != null)
        {
            var g = Regions.Definition.Geometry;
            WorldBounds = Rect.MinMaxRect((float)g.MinimumX, (float)g.MinimumY, (float)g.MaximumX, (float)g.MaximumY); return;
        }
        Rect r = new(PlayerPosition - Vector2.one * Style.MinimumMapSpan * .5f, Vector2.one * Style.MinimumMapSpan);
        foreach (var row in _rows) { var p = Point(row); r = Rect.MinMaxRect(Mathf.Min(r.xMin, p.x - 8), Mathf.Min(r.yMin, p.y - 8), Mathf.Max(r.xMax, p.x + 8), Mathf.Max(r.yMax, p.y + 8)); }
        WorldBounds = r;
    }
    private Rect OccupiedBounds()
    {
        var result = new Rect(PlayerPosition, Vector2.zero);
        if (Regions == null) return result;
        var g = Regions.Definition.Geometry;
        for (int y = 0; y < g.CellsY; y++) for (int x = 0; x < g.CellsX; x++)
        {
            if (!Regions.IsCaptured(x, y)) continue;
            float minX = (float)(g.MinimumX + x * g.CellWidth), minY = (float)(g.MinimumY + y * g.CellHeight);
            result = Rect.MinMaxRect(Mathf.Min(result.xMin, minX), Mathf.Min(result.yMin, minY),
                Mathf.Max(result.xMax, minX + (float)g.CellWidth), Mathf.Max(result.yMax, minY + (float)g.CellHeight));
        }
        return result;
    }
    private void CaptureEnemies()
    {
        if (Time.unscaledTime >= _nextEnemyDiscovery)
        { _nextEnemyDiscovery = Time.unscaledTime + .75f; _enemySources = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None); }
        _enemies.Clear(); _buckets.Clear();
        float bucketSize = Mathf.Max(6, (IsOpen ? _size : (OutputCamera != null ? OutputCamera.orthographicSize : 20)) / 4);
        foreach (var enemy in _enemySources)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.gameObject.scene != gameObject.scene || enemy.CurrentHealth <= 0) continue;
            Vector2 p = enemy.transform.position; if (!float.IsFinite(p.x) || !float.IsFinite(p.y)) continue;
            _enemies.Add(p); var cell = new Vector2Int(Mathf.FloorToInt(p.x / bucketSize), Mathf.FloorToInt(p.y / bucketSize));
            _buckets.TryGetValue(cell, out var b); _buckets[cell] = (b.sum + p, b.count + 1);
        }
        _groups.Clear(); foreach (var b in _buckets.Values) _groups.Add(new EnemyGroup(b.sum / b.count, b.count));
        _groups.Sort((a, b) => b.Count.CompareTo(a.Count));
    }
    public bool CanOperate(TurretPresentationSnapshot row)
    {
        if (row == null || IsTransitioning || !Source.CanOperateTurrets) return false;
        bool captured = Regions != null && Regions.TryIsCapturedAt(row.Position, out bool c) && c;
        return TacticalPresentationRules.CanReach(true,IsOpen,captured,Vector2.Distance(PlayerPosition,Point(row)),Source.InteractionDistance);
    }
    public bool TryControlPosition(out Vector2 point)
    {
        var controller = Source.Commands?.Presentation.Snapshot?.Controller;
        if (controller != null && controller.IsControlUnitAvailable)
        { point = new Vector2((float)controller.ControlUnitPosition.X, (float)controller.ControlUnitPosition.Y); return true; }
        if (!Source.RequiresCommands && Source.ControlUnit != null) { point = Source.ControlUnit.transform.position; return true; }
        point = default; return false;
    }
    private TacticalPresentationRules.Vitality _playerVitality=new(),_cuVitality=new();
    public TacticalPresentationRules.Vitality PlayerVitality=>_playerVitality;
    public TacticalPresentationRules.Vitality ControlVitality=>_cuVitality;
    public float HealthTrail(TurretPresentationSnapshot row)=>_damage.TryGetValue(row.EntityId,out var v)?v.Trail(Time.unscaledTime):row.CurrentHealth/(float)Mathf.Max(1,row.MaxHealth);
    public Vector2 HitOffset(EntityId id)=>_damage.TryGetValue(id,out var v)?v.Offset(Time.unscaledTime):Vector2.zero;
    public float HitImpact(EntityId id)=>_damage.TryGetValue(id,out var v)?v.Impact(Time.unscaledTime):0;
}

using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private readonly Dictionary<EntityId, TacticalPresentationRules.Vitality> _damage = new();
    private readonly List<Vector2> _enemies = new();
    private readonly List<EnemyGroup> _groups = new();
    private readonly List<(Vector2 point, int count)> _labelGroups = new();
    private readonly List<Vector2> _placedEnemyLabels = new();
    private static readonly Vector2[] EnemyLabelOffsets={Vector2.zero,new(0,36),new(0,-36),new(72,0),new(-72,0),new(0,60)};
    private readonly Vector3[] _uiCorners = new Vector3[4];
    private readonly List<EntityId> _incidents = new();
    private readonly Dictionary<Vector2Int, (Vector2 sum, int count)> _buckets = new();
    private EnemyHealth[] _enemySources = System.Array.Empty<EnemyHealth>();
    private float _nextEnemyDiscovery;
    private AlertStripView _visibleAlert;
    private float _nextAlertSearch;
    private Texture2D _regionTexture;
    private System.Guid _rasterGrid;
    private long _rasterRevision = -1;
    private int _regionMaskHash;
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
    private void CaptureData()
    {
        _rows.Clear(); _byId.Clear(); _locals.Clear(); Regions = null; Battlefield=null; _captureProgress.Clear();
        if (_player == null || Source.Turrets == null) { _enemies.Clear(); _groups.Clear(); return; }
        foreach (var turret in FieldTurretInstanceRegistry.GetAll())
        {
            if (turret == null || !Source.Turrets.OwnsTurret(turret) || turret.Definition == null) continue;
            if (turret.TryGetEntityId(out var id)) _locals[id] = turret;
            else if (!Source.RequiresCommands) _locals[new EntityId("local/" + turret.InstanceId)] = turret;
        }
        var frame = Source.Commands?.Presentation.Snapshot;
        if (Source.RequiresCommands)
        {
            if (frame == null || frame.MatchId != Source.BoundMatchId)
            {
                _enemies.Clear(); _groups.Clear(); _incidents.Clear(); _selected = default; _batch.Clear();
                foreach (var button in View.Incidents) button.gameObject.SetActive(false);
                RefreshRegionRaster(); RefreshIntelLabels(); return;
            }
            _rows.AddRange(frame.Turrets);
        }
        else foreach (var item in _locals)
        {
            var t = item.Value; var flags = TurretPresentationFlags.None;
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
        foreach (var row in _rows)
        {
            _byId[row.EntityId] = row;
            if (!_damage.TryGetValue(row.EntityId, out var sample)) _damage[row.EntityId] = sample = new TacticalPresentationRules.Vitality();
            sample.Sample(row.CurrentHealth,row.MaxHealth,Time.unscaledTime);
        }
        foreach (var id in _damage.Keys.Where(id => !_byId.ContainsKey(id)).ToArray()) _damage.Remove(id);
        _batch.RemoveWhere(id => !_byId.ContainsKey(id));
        if (!_byId.ContainsKey(_selected))
        {
            _selected=default;
            foreach(var remaining in _batch){_selected=remaining;break;}
        }
        var regions = Source.Turrets.Regions?.Snapshot;
        if (regions != null && (!Source.RequiresCommands || (regions.MatchId == _match &&
            regions.GridId == frame.Controller.RegionGridId && regions.Revision >= frame.Controller.RegionRevision))) Regions = regions;
        _playerVitality.Sample(_player.CurrentHealth,_player.MaxHealth,Time.unscaledTime);
        var cuFrame=frame?.Controller;
        if(Source.RequiresCommands && cuFrame!=null && cuFrame.IsControlUnitAvailable)
            _cuVitality.Sample(cuFrame.ControlUnitHealth,cuFrame.ControlUnitMaxHealth,Time.unscaledTime);
        else if(!Source.RequiresCommands && Source.ControlUnit!=null)
            _cuVitality.Sample(Source.ControlUnit.CurrentHealth,Source.ControlUnit.MaxHealth,Time.unscaledTime);
        CaptureIntel(); UpdateBounds(); CaptureEnemies(); RefreshIncidents(); RefreshRegionRaster();
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
    private TurretPresentationSnapshot Pick(Vector2 screen, float pixels)
    {
        var camera = OutputCamera; if (camera == null) return null;
        TurretPresentationSnapshot best = null; float distance = pixels * pixels;
        foreach (var row in _rows)
        {
            var point = camera.WorldToScreenPoint(Point(row)); if (point.z < 0) continue;
            float d = ((Vector2)point - screen).sqrMagnitude;
            if (d > distance) continue; distance = d; best = row;
        }
        return best;
    }
    private void RefreshHover()
    {
        bool overUi = GameInput.IsPointerOverUi();
        var target = overUi ? null : Pick(GameInput.PointerPosition, 42);
        var id = target != null ? target.EntityId : default;
        if (_hovered != id) { _hovered = id; _hoverSince = Time.unscaledTime; }
        bool quiet = Time.unscaledTime - _lastShot >= Style.ShotQuietSeconds && !GameInput.PrimaryPointerIsPressed;
        bool information = target != null && quiet && Time.unscaledTime - _hoverSince >= Style.HoverDelay;
        View.Tooltip.gameObject.SetActive(information);
        if (information)
        {
            Sprite art = SpriteFor(target); View.TooltipIcon.sprite = art; View.TooltipIcon.enabled = art != null;
            View.TooltipLabel.text=Caption(target);
            View.TooltipPower.text=target.Power.ToString();
            View.TooltipState.Set(TacticalSymbolGraphic.ForState(StateOf(target)),StateColor(StateOf(target)));
            SetHealth(View.TooltipHealth,target);
            PlaceClamped(View.Tooltip, GameInput.PointerPosition + new Vector2(24, 28));
        }
        // 기존 F 구조와 플레이어가 설치한 포탑 회수를 먼저 처리합니다. 같은 입력으로 두 행동을 실행하지 않습니다.
        var skills=_player.GetComponent<PlayerSkillController>();
        bool reserved=_player.HasRescueTarget || (skills!=null && skills.CanRecover && skills.RecoverableTurret!=null);
        var nearby=target!=null && Vector2.Distance(PlayerPosition,Point(target))<=Source.InteractionDistance?target:null;
        float nearest=Source.InteractionDistance;
        if(nearby==null)foreach(var row in _rows)
        {
            float distance=Vector2.Distance(PlayerPosition,Point(row));
            if(distance<=nearest && StageTurretInteractionRules.CanToggle(row)){nearest=distance;nearby=row;}
        }
        bool near = !reserved && Source.CanOperateTurrets && nearby != null && StageTurretInteractionRules.CanToggle(nearby) && !Pending(nearby);
        View.Prompt.gameObject.SetActive(near);
        if (near)
        {
            View.PromptLabel.text = "F"; View.PromptAction.sprite = Style.PowerGlyph;
            View.PromptAction.color = nearby.IsActivated ? Style.Common.Health : StateColor(StateOf(nearby));
            PlaceClamped(View.Prompt, (Vector2)OutputCamera.WorldToScreenPoint(Point(nearby)) + Vector2.down * 44);
            if (GameInput.WasPressedThisFrame(GameKey.F)) { _selected = nearby.EntityId; ToggleSelected(); }
        }
    }
    private void PlaceClamped(RectTransform rect, Vector2 screen)
    {
        var root = (RectTransform)View.transform; Vector2 p = CanvasPoint(screen); Vector2 half = Vector2.Scale(rect.rect.size,rect.localScale) * .5f;
        p.x = Mathf.Clamp(p.x, root.rect.xMin + half.x + 16, root.rect.xMax - half.x - 16);
        p.y = Mathf.Clamp(p.y, root.rect.yMin + half.y + 16, root.rect.yMax - half.y - 16); rect.anchoredPosition = p;
    }
    public string Caption(TurretPresentationSnapshot row)
    {
        string id = row.DefinitionId.Value.ToLowerInvariant();
        string kind = id.Contains("missile") ? "미사일" : id.Contains("laser") ? "레이저" : id.Contains("canon") || id.Contains("cannon") ? "캐논" : row.DisplayName;
        return $"T-{_rows.IndexOf(row) + 1:00}  {kind} Lv.{row.Level}";
    }
    public bool TryControlPosition(out Vector2 point)
    {
        var controller = Source.Commands?.Presentation.Snapshot?.Controller;
        if (controller != null && controller.IsControlUnitAvailable)
        { point = new Vector2((float)controller.ControlUnitPosition.X, (float)controller.ControlUnitPosition.Y); return true; }
        if (!Source.RequiresCommands && Source.ControlUnit != null) { point = Source.ControlUnit.transform.position; return true; }
        point = default; return false;
    }
    private void RefreshIncidents()
    {
        // 포인터가 목록에 있는 동안 순서를 유지해 클릭 직전 대상이 바뀌지 않게 합니다.
        bool keep = IsOpen && View.Incidents.Any(b => RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, GameInput.PointerPosition));
        if (!keep)
        {
            _incidents.Clear();
            foreach (var r in _rows.Where(r => WasHit(r.EntityId)).OrderBy(r => r.CurrentHealth / (float)Mathf.Max(1, r.MaxHealth))) _incidents.Add(r.EntityId);
            foreach (var r in _rows.Where(r => !r.IsActivated && !r.IsLocked && !r.IsDestroyed && EnemyCount(r) > 0))
                if (!_incidents.Contains(r.EntityId)) _incidents.Add(r.EntityId);
        }
        for (int i = 0; i < View.Incidents.Length; i++)
        {
            bool show = i < _incidents.Count && _byId.TryGetValue(_incidents[i], out _);
            View.Incidents[i].gameObject.SetActive(show); if (!show) continue;
            var row = _byId[_incidents[i]];
            View.IncidentIcons[i].sprite=Style.GlyphFor(row.DefinitionId.Value);
            View.IncidentStates[i].Set(WasHit(row.EntityId)?TacticalSymbolGraphic.Symbol.Impact:TacticalSymbolGraphic.Symbol.Threat,Style.Enemy);
            SetHealth(View.IncidentHealth[i],row);
        }
    }
    private void FocusIncident(int index)
    {
        if (index >= _incidents.Count || !_byId.TryGetValue(_incidents[index], out var row)) return;
        Focus(Point(row)); Select(row.EntityId);
    }
    private void RefreshGoal(bool field)
    {
        bool show = field && !IsOpen && _hasGoal && OutputCamera != null &&
            _player.GetComponent<PlayerSkillController>()?.IsSelectionOpen!=true &&
            !(_app!=null && _app.IsInitialized && _app.LocalInput.IsBlocked);
        View.Goal.gameObject.SetActive(show); if (!show) return;
        var root = (RectTransform)View.transform;
        Vector2 target = CanvasPoint(OutputCamera.WorldToScreenPoint(_goal));
        float readable=1/Mathf.Min(1,Mathf.Max(.1f,View.Canvas.rootCanvas.scaleFactor));
        // HP/CU 표시, 상단 시간, 하단 무기/스킬의 안전 영역 안에서만 방향을 배치합니다.
        var bounds = Rect.MinMaxRect(root.rect.xMin+Mathf.Min(110*readable,root.rect.width*.18f),
            root.rect.yMin+Mathf.Min(250*readable,root.rect.height*.42f),
            root.rect.xMax-Mathf.Min(150*readable,root.rect.width*.22f),root.rect.yMax-Mathf.Min(110*readable,root.rect.height*.2f));
        Vector2 point = target; bool offscreen = !bounds.Contains(target);
        if (offscreen)
        {
            float scale = Mathf.Min(bounds.width * .5f / Mathf.Max(.01f, Mathf.Abs(target.x - bounds.center.x)),
                bounds.height * .5f / Mathf.Max(.01f, Mathf.Abs(target.y - bounds.center.y)));
            point = bounds.center + (target - bounds.center) * scale;
        }
        View.Goal.anchoredPosition = point;
        View.GoalPin.enabled = !offscreen && Style.GoalGlyph != null;
        View.GoalArrow.enabled = offscreen || Style.GoalGlyph == null;
        if(_visibleAlert!=null && _visibleAlert.Group.alpha>.001f)
        {
            ((RectTransform)_visibleAlert.transform).GetWorldCorners(_uiCorners);
            var a=CanvasPoint(RectTransformUtility.WorldToScreenPoint(null,_uiCorners[0]));
            var b=CanvasPoint(RectTransformUtility.WorldToScreenPoint(null,_uiCorners[2]));
            if(new Rect(point-new Vector2(50,30)*readable,new Vector2(100,60)*readable).Overlaps(Rect.MinMaxRect(a.x,a.y,b.x,b.y)))
            {offscreen=true;point.x=target.x>=bounds.center.x?bounds.xMax:bounds.xMin;View.Goal.anchoredPosition=point;View.GoalPin.enabled=false;View.GoalArrow.enabled=true;}
        }
        Vector2 direction=target-CanvasPoint(OutputCamera.WorldToScreenPoint(PlayerPosition));
        View.GoalArrow.rectTransform.localEulerAngles = new Vector3(0, 0, offscreen ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90 : 0);
        float t=Mathf.Clamp01((Time.unscaledTime-_goalSetAt)/.22f);t=t*t*(3-2*t);
        View.Goal.localScale=Vector3.one*readable*Mathf.Lerp(.85f,1,t);
        View.GoalLabel.text = $"{Vector2.Distance(PlayerPosition, _goal):0}m";
        if (Vector2.Distance(PlayerPosition, _goal) < 3) ClearGoal();
    }
    /// <summary>카메라 적용 이후의 좌표로 UI를 배치합니다.</summary>
    public void RenderVisuals()
    {
        if (View == null) return;
        float readableScale=1/Mathf.Min(1,Mathf.Max(.1f,View.Canvas.rootCanvas.scaleFactor));
        if (IsOpen)
        {
            RenderViewport();
            RenderTurretGlyphs();
            RenderTacticalDetails();
            if(_visibleAlert==null && Time.unscaledTime>=_nextAlertSearch)
            { _nextAlertSearch=Time.unscaledTime+.5f; _visibleAlert=FindFirstObjectByType<AlertStripView>(); }
            _labelGroups.Clear();
            foreach (var group in _groups)
            {
                if(!ShowEnemyGlyphs)break;
                if(group.Count<2)continue;
                var screen = OutputCamera.WorldToScreenPoint(group.Position);
                if (screen.z < 0 || !IsInMapViewport(screen) || screen.x < 40 || screen.x > Screen.width - 80 || screen.y < 140 || screen.y > Screen.height - 70) continue;
                Vector2 p = WorldCanvasPoint(screen) + new Vector2(10, 16); bool merged = false;
                for (int i = 0; i < _labelGroups.Count; i++)
                {
                    var other = _labelGroups[i];
                    if (Mathf.Abs(other.point.x - p.x) >= 100*readableScale || Mathf.Abs(other.point.y - p.y) >= 34*readableScale) continue;
                    _labelGroups[i] = ((other.point * other.count + p * group.Count) / (other.count + group.Count), other.count + group.Count);
                    merged = true; break;
                }
                if (!merged && _labelGroups.Count < Mathf.Min(Style.MaximumEnemyGroups, View.EnemyLabels.Length)) _labelGroups.Add((p, group.Count));
            }
            // 병합으로 중심이 옮겨지며 다시 겹친 묶음도 하나로 합칩니다. 합계는 실제 개체 수입니다.
            for (int i = 0; i < _labelGroups.Count; i++) for (int j = i + 1; j < _labelGroups.Count; j++)
            {
                var a = _labelGroups[i]; var b = _labelGroups[j];
                if (Mathf.Abs(a.point.x - b.point.x) >= 100*readableScale || Mathf.Abs(a.point.y - b.point.y) >= 34*readableScale) continue;
                _labelGroups[i] = ((a.point * a.count + b.point * b.count) / (a.count + b.count), a.count + b.count);
                _labelGroups.RemoveAt(j); i = -1; break;
            }
            int used = 0;
            _placedEnemyLabels.Clear();
            foreach (var group in _labelGroups)
            {
                if (!TryPlaceEnemyLabel(group.point,readableScale,out var point)) continue;
                _placedEnemyLabels.Add(point);
                var icon = View.EnemyIcons[used]; icon.gameObject.SetActive(Style.EnemyGlyph != null);
                icon.rectTransform.sizeDelta=Vector2.one*20*readableScale;
                icon.rectTransform.anchoredPosition = point + new Vector2(-24, 0)*readableScale;
                var label = View.EnemyLabels[used++]; label.gameObject.SetActive(true); label.text = group.count.ToString();
                label.fontSize=Mathf.Max(16,13*readableScale);
                label.rectTransform.sizeDelta=new Vector2(40,26)*readableScale;
                label.rectTransform.anchoredPosition = point + new Vector2(14, 0)*readableScale;
            }
            for (int i = used; i < View.EnemyLabels.Length; i++) { View.EnemyLabels[i].gameObject.SetActive(false); View.EnemyIcons[i].gameObject.SetActive(false); }
            Vector3 playerScreen = OutputCamera.WorldToScreenPoint(PlayerPosition);
            View.PlayerMarker.gameObject.SetActive(IsInMapViewport(playerScreen));
            View.PlayerMarker.rectTransform.sizeDelta=Vector2.one*24*readableScale;
            View.PlayerMarker.rectTransform.anchoredPosition = WorldCanvasPoint(playerScreen)+_playerVitality.Offset(Time.unscaledTime)*readableScale;
            bool cu = TryControlPosition(out var cuPoint) && IsInMapViewport(OutputCamera.WorldToScreenPoint(cuPoint)); View.ControlMarker.gameObject.SetActive(cu);
            if (cu)
            {
                View.ControlMarker.rectTransform.sizeDelta=Vector2.one*24*readableScale;
                View.ControlMarker.rectTransform.anchoredPosition = WorldCanvasPoint(OutputCamera.WorldToScreenPoint(cuPoint)) + Vector2.up * (24*readableScale)+_cuVitality.Offset(Time.unscaledTime)*readableScale;
            }
        }
        if(Regions!=null && View.TerritoryFill.texture!=null)
        {
            var g=Regions.Definition.Geometry;
            Vector2 a=WorldCanvasPoint(OutputCamera.WorldToScreenPoint(new Vector2((float)g.MinimumX,(float)g.MinimumY)));
            Vector2 b=WorldCanvasPoint(OutputCamera.WorldToScreenPoint(new Vector2((float)g.MaximumX,(float)g.MaximumY)));
            View.TerritoryFill.rectTransform.anchoredPosition=(a+b)*.5f;View.TerritoryFill.rectTransform.sizeDelta=b-a;
        }
        View.WorldGraphic.SetVerticesDirty(); View.OverviewGraphic.SetVerticesDirty();
    }
    private TacticalPresentationRules.Vitality _playerVitality=new(),_cuVitality=new();
    public TacticalPresentationRules.Vitality PlayerVitality=>_playerVitality;
    public TacticalPresentationRules.Vitality ControlVitality=>_cuVitality;
    public float HealthTrail(TurretPresentationSnapshot row)=>_damage.TryGetValue(row.EntityId,out var v)?v.Trail(Time.unscaledTime):row.CurrentHealth/(float)Mathf.Max(1,row.MaxHealth);
    public Vector2 HitOffset(EntityId id)=>_damage.TryGetValue(id,out var v)?v.Offset(Time.unscaledTime):Vector2.zero;
    public float HitImpact(EntityId id)=>_damage.TryGetValue(id,out var v)?v.Impact(Time.unscaledTime):0;
    private Color HealthColor(float fraction)=>fraction<.3f?Style.Enemy:fraction<.6f?Style.Common.Power:Style.Active;
    private void SetHealth(TacticalHealthGraphic graphic,TurretPresentationSnapshot row)
    {float fraction=row.CurrentHealth/(float)Mathf.Max(1,row.MaxHealth);graphic.Set(fraction,HealthTrail(row),HealthColor(fraction));}
    private void RenderTacticalDetails()
    {
        var row=Selected;
        if(row!=null && _batch.Count<=1)
        {
            SetHealth(View.DockHealth,row);
            View.TurretIcon.rectTransform.anchoredPosition=new Vector2(8,-12)+HitOffset(row.EntityId);
            View.DockState.rectTransform.anchoredPosition=new Vector2(104,-75)+HitOffset(row.EntityId);
        }
        for(int i=0;i<View.Incidents.Length;i++)
            if(View.Incidents[i].gameObject.activeSelf && i<_incidents.Count && _byId.TryGetValue(_incidents[i],out var incident))
            {
                SetHealth(View.IncidentHealth[i],incident);
                View.IncidentIcons[i].rectTransform.anchoredPosition=new Vector2(10,-5)+HitOffset(incident.EntityId);
                View.IncidentStates[i].rectTransform.anchoredPosition=new Vector2(-12,-9)+HitOffset(incident.EntityId);
            }
        View.Back.interactable=_hasPreviousView;
        View.FocusCU.interactable=TryControlPosition(out _);
        View.FocusPlayer.GetComponent<TeamHJD.Game.Presentation.UiButtonFeedback>().Icon.color=
            _playerVitality.Impact(Time.unscaledTime)>0?Style.Enemy:Style.Common.Text;
        View.FocusCU.GetComponent<TeamHJD.Game.Presentation.UiButtonFeedback>().Icon.color=
            _cuVitality.Impact(Time.unscaledTime)>0?Style.Enemy:Style.Common.Text;
    }
    private void RenderTurretGlyphs()
    {
        float zoom = _baseSize > 0 ? _size / _baseSize : 1;
        if (_showGlyphs ? zoom < Style.GlyphLeaveZoom : zoom > Style.GlyphEnterZoom) _showGlyphs = !_showGlyphs;
        _glyphAlpha = Mathf.MoveTowards(_glyphAlpha, _showGlyphs ? 1 : 0, Time.unscaledDeltaTime / .12f);
        _visibleGlyphs.Clear(); int used = 0;
        foreach (var row in _rows)
        {
            if (used >= View.TurretGlyphs.Length || _glyphAlpha <= .001f) break;
            var point = OutputCamera.WorldToScreenPoint(Point(row));
            if (point.z < 0 || !IsInMapViewport(point)) continue;
            Sprite sprite = Style.GlyphFor(row.DefinitionId.Value); if (sprite == null) continue;
            var icon = View.TurretGlyphs[used++]; icon.gameObject.SetActive(true); icon.sprite = sprite;
            var state=StateOf(row);
            Color tone = state==TacticalPresentationRules.TurretState.Active?Color.Lerp(Style.Common.Text,Style.Active,.3f):
                state==TacticalPresentationRules.TurretState.Off?Style.Common.Text:StateColor(state);
            tone.a = _glyphAlpha; icon.color = tone;
            icon.rectTransform.anchoredPosition = WorldCanvasPoint(point)+HitOffset(row.EntityId)/Mathf.Min(1,Mathf.Max(.1f,View.Canvas.rootCanvas.scaleFactor));
            icon.rectTransform.sizeDelta = Vector2.one * Style.GlyphSize / Mathf.Min(1,Mathf.Max(.1f,View.Canvas.rootCanvas.scaleFactor));
            float targetScale=IsSelected(row.EntityId)?1.1f:row.EntityId==_hovered?1.07f:1;
            icon.rectTransform.localScale=Vector3.Lerp(icon.rectTransform.localScale,Vector3.one*targetScale,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            if (_glyphAlpha > .5f) _visibleGlyphs.Add(row.EntityId);
        }
        for (int i = used; i < View.TurretGlyphs.Length; i++) View.TurretGlyphs[i].gameObject.SetActive(false);
    }
    private bool LabelCovered(Vector2 point)
    {
        point = ((RectTransform)View.transform).InverseTransformPoint(View.WorldClip.TransformPoint(point));
        float scale=1/Mathf.Min(1,Mathf.Max(.1f,View.Canvas.rootCanvas.scaleFactor));
        var label = new Rect(point - new Vector2(48, 18)*scale, new Vector2(96, 36)*scale);
        // 수량 카드가 터렛 도상·상태·HP와 Player/CU 표식을 가리지 않게 합니다.
        foreach(var row in _rows)
        {
            Vector2 p=CanvasPoint(OutputCamera.WorldToScreenPoint(Point(row)));
            if(label.Overlaps(new Rect(p-new Vector2(24,44)*scale,new Vector2(48,70)*scale)))return true;
        }
        Vector2 player=CanvasPoint(OutputCamera.WorldToScreenPoint(PlayerPosition));
        if(label.Overlaps(new Rect(player-new Vector2(24,28)*scale,new Vector2(48,52)*scale)))return true;
        if(TryControlPosition(out var cu))
        {
            Vector2 p=CanvasPoint(OutputCamera.WorldToScreenPoint(cu));
            if(label.Overlaps(new Rect(p-new Vector2(30,30)*scale,new Vector2(60,70)*scale)))return true;
        }
        if (Covers(View.Dock) || Covers(View.UpgradeDrawer) || Covers((RectTransform)View.Overview.parent) || Covers((RectTransform)View.Close.transform) ||
            Covers(View.SettingsDrawer) || Covers((RectTransform)View.MapSettings.transform)) return true;
        foreach (var b in View.Incidents) if (Covers((RectTransform)b.transform)) return true;
        if(Covers((RectTransform)View.AutoActivate.transform) || Covers((RectTransform)View.BatchActivate.transform) ||
            Covers((RectTransform)View.BatchDeactivate.transform) || Covers((RectTransform)View.SelectMultiple.transform) ||
            Covers((RectTransform)View.FocusPlayer.transform) || Covers((RectTransform)View.FocusCU.transform) ||
            Covers((RectTransform)View.Back.transform))return true;
        if(_visibleAlert!=null && _visibleAlert.Group.alpha>.001f && Covers((RectTransform)_visibleAlert.transform))return true;
        return false;
        bool Covers(RectTransform r)
        {
            if (!r.gameObject.activeInHierarchy) return false;
            r.GetWorldCorners(_uiCorners);
            Vector2 a = CanvasPoint(RectTransformUtility.WorldToScreenPoint(null, _uiCorners[0]));
            Vector2 b = CanvasPoint(RectTransformUtility.WorldToScreenPoint(null, _uiCorners[2]));
            return label.Overlaps(Rect.MinMaxRect(a.x, a.y, b.x, b.y));
        }
    }
    private bool TryPlaceEnemyLabel(Vector2 origin,float scale,out Vector2 point)
    {
        foreach(var offset in EnemyLabelOffsets)
        {
            point=origin+offset*scale;
            var area=View.WorldClip.rect;
            if(!area.Contains(point-new Vector2(48,18)*scale) || !area.Contains(point+new Vector2(48,18)*scale) || LabelCovered(point))continue;
            bool overlaps=false;
            foreach(var other in _placedEnemyLabels)
                if(Mathf.Abs(other.x-point.x)<100*scale && Mathf.Abs(other.y-point.y)<38*scale){overlaps=true;break;}
            if(!overlaps)return true;
        }
        point=default;return false;
    }
    private void RenderViewport()
    {
        var canvas = ((RectTransform)View.transform).rect;
        Rect pixels = OutputCamera.pixelRect;
        Vector2 a = CanvasPoint(pixels.min), b = CanvasPoint(pixels.max);
        View.WorldClip.anchoredPosition = (a + b) * .5f; View.WorldClip.sizeDelta = b - a;
        Set(View.ViewportMatte[0],Rect.MinMaxRect(canvas.xMin,canvas.yMin,a.x,canvas.yMax));
        Set(View.ViewportMatte[1],Rect.MinMaxRect(b.x,canvas.yMin,canvas.xMax,canvas.yMax));
        Set(View.ViewportMatte[2],Rect.MinMaxRect(a.x,canvas.yMin,b.x,a.y));
        Set(View.ViewportMatte[3],Rect.MinMaxRect(a.x,b.y,b.x,canvas.yMax));
        static void Set(RectTransform target, Rect r)
        { target.gameObject.SetActive(r.width>.01f && r.height>.01f); target.anchoredPosition=r.center; target.sizeDelta=r.size; }
    }
    private void RefreshRegionRaster()
    {
        bool terrain = Style.OverviewTerrain != null && gameObject.scene.path == Style.TerrainScene &&
            Mathf.Abs(WorldBounds.x - Style.TerrainBounds.x) < .01f && Mathf.Abs(WorldBounds.y - Style.TerrainBounds.y) < .01f &&
            Mathf.Abs(WorldBounds.width - Style.TerrainBounds.width) < .01f && Mathf.Abs(WorldBounds.height - Style.TerrainBounds.height) < .01f;
        View.OverviewTerrain.enabled = terrain;
        if (terrain)
        {
            View.OverviewTerrain.texture = Style.OverviewTerrain;
            Rect terrainFit = TacticalMapMath.Fit(View.Overview.rect, WorldBounds);
            View.OverviewTerrain.rectTransform.anchoredPosition = terrainFit.center; View.OverviewTerrain.rectTransform.sizeDelta = terrainFit.size;
        }
        View.OverviewFill.enabled = Regions != null;View.TerritoryFill.enabled=Regions!=null;
        if (Regions == null) return;
        var g = Regions.Definition.Geometry;
        if (_regionTexture == null || _regionTexture.width != g.CellsX*8 || _regionTexture.height != g.CellsY*8)
        {
            if (_regionTexture != null) Destroy(_regionTexture);
            _regionTexture = new Texture2D(g.CellsX*8, g.CellsY*8, TextureFormat.RGBA32, false) { name = "Tactical captured cells", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _rasterRevision = -1;
        }
        int stateHash=17;for(int i=0;i<g.TotalCellCount;i++)stateHash=unchecked(stateHash*31+(int)StateOfCell(i));
        if (_rasterGrid != Regions.GridId || _rasterRevision != Regions.Revision || _regionMaskHash!=stateHash)
        {
            var pixels = new Color32[_regionTexture.width*_regionTexture.height];
            for(int y=0;y<g.CellsY;y++)for(int x=0;x<g.CellsX;x++)
            {
                var state=StateOfCell(Regions.Definition.GetCellIndex(x,y));
                Color32 c=state switch {TacticalPresentationRules.CellState.Captured=>new Color32(255,0,0,0),TacticalPresentationRules.CellState.Available=>new Color32(0,255,0,0),TacticalPresentationRules.CellState.Capturing=>new Color32(0,0,255,0),_=>new Color32(0,0,0,255)};
                for(int yy=0;yy<8;yy++)for(int xx=0;xx<8;xx++)pixels[(y*8+yy)*_regionTexture.width+x*8+xx]=c;
            }
            _regionMaskHash=stateHash;
            _regionTexture.SetPixels32(pixels); _regionTexture.Apply(false, false); _rasterGrid = Regions.GridId; _rasterRevision = Regions.Revision;
        }
        View.OverviewFill.texture = _regionTexture;View.TerritoryFill.texture=_regionTexture;
        Rect regionBounds = Rect.MinMaxRect((float)g.MinimumX, (float)g.MinimumY, (float)g.MaximumX, (float)g.MaximumY);
        bool visible = TacticalMapMath.TryRasterPlacement(regionBounds, WorldBounds, View.Overview.rect, out Rect fit, out Rect uv);
        View.OverviewFill.enabled = visible;
        if (visible)
        {
            View.OverviewFill.uvRect = uv;
            View.OverviewFill.rectTransform.anchoredPosition = fit.center; View.OverviewFill.rectTransform.sizeDelta = fit.size;
        }
    }
}

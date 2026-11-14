using System.Collections.Generic;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using UnityEngine;

public sealed partial class TacticalCommandController
{
    [Header("Map-only scenery visibility (Scene references)")]
    public Renderer[] SceneryRenderers = System.Array.Empty<Renderer>();
    public BattlefieldSpatialSnapshot Battlefield { get; private set; }
    public bool ShowAllRanges { get; private set; } = true;
    public bool ShowScenery { get; private set; } = true;
    public bool ShowEnemyGlyphs => _showGlyphs;
    private RegionSceneBinding _regionBinding;
    private InitialStageCompositionRoot _composition;
    private readonly Dictionary<int, StaticRegionCaptureSnapshot> _captureProgress = new();
    private readonly Dictionary<Renderer, bool> _hiddenScenery = new();
    private const string RangesKey = "TheDeveloper.Tactical.AllRanges.v1";
    private const string SceneryKey = "TheDeveloper.Tactical.Scenery.v1";

    public TacticalPresentationRules.TurretState StateOf(TeamHJD.Game.Contracts.TurretPresentationSnapshot row)
        => TacticalPresentationRules.State(row, AvailablePower, Pending(row));
    private bool TerritoryLocked(TeamHJD.Game.Contracts.TurretPresentationSnapshot row)
        => IsOpen && row != null && Regions != null && !(Regions.TryIsCapturedAt(row.Position,out bool captured) && captured);
    public TacticalPresentationRules.CellState StateOfCell(int index)
    {
        _captureProgress.TryGetValue(Regions.Definition.RegionIds[index], out var progress);
        return TacticalPresentationRules.Cell(Regions, index, progress);
    }
    public Color StateColor(TacticalPresentationRules.TurretState state) => state switch
    {
        TacticalPresentationRules.TurretState.Active => Style.Active,
        TacticalPresentationRules.TurretState.Off => Style.Inactive,
        TacticalPresentationRules.TurretState.Destroyed => Style.Common.Health,
        TacticalPresentationRules.TurretState.Cooling => Style.Common.Power,
        TacticalPresentationRules.TurretState.NoPower => Style.Common.Power,
        TacticalPresentationRules.TurretState.Busy => Style.Common.Accent,
        _ => new Color(.66f,.68f,.66f)
    };
    public Color CellColor(TacticalPresentationRules.CellState state) => state switch
    {
        TacticalPresentationRules.CellState.Captured => Style.CapturedFill,
        TacticalPresentationRules.CellState.Capturing => Style.CapturingFill,
        TacticalPresentationRules.CellState.Available => Style.AvailableFill,
        _ => Style.UnoccupiedFill
    };
    private void InitializeMapSettings()
    {
        ShowAllRanges = PlayerPrefs.GetInt(RangesKey,1) != 0;
        ShowScenery = PlayerPrefs.GetInt(SceneryKey,1) != 0;
        View.MapSettings.onClick.AddListener(() => {
            View.SettingsDrawer.gameObject.SetActive(!View.SettingsDrawer.gameObject.activeSelf);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        });
        View.Ranges.onClick.AddListener(() => SetMapVisibility(!ShowAllRanges,ShowScenery));
        View.Scenery.onClick.AddListener(() => SetMapVisibility(ShowAllRanges,!ShowScenery));
        View.ClearGoal.onClick.AddListener(ClearGoal);
        RefreshMapSettings();
    }
    public void SetMapVisibility(bool allRanges, bool scenery, bool save = true)
    {
        ShowAllRanges=allRanges; ShowScenery=scenery;
        if(save) { PlayerPrefs.SetInt(RangesKey,allRanges?1:0); PlayerPrefs.SetInt(SceneryKey,scenery?1:0); PlayerPrefs.Save(); }
        ApplySceneryVisibility(); RefreshMapSettings();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }
    private void RefreshMapSettings()
    {
        View.RangeLabel.text="사거리";
        View.RangeState.Set(ShowAllRanges?TacticalSymbolGraphic.Symbol.RangeAll:TacticalSymbolGraphic.Symbol.RangeSelected,ShowAllRanges?Style.Common.Accent:Style.Common.Text);
        View.SceneryLabel.text="지형지물";
        View.SceneryState.Set(ShowScenery?TacticalSymbolGraphic.Symbol.TerrainVisible:TacticalSymbolGraphic.Symbol.TerrainHidden,ShowScenery?Style.Common.Accent:Style.Common.Text);
        View.RangeLabel.color=ShowAllRanges?Style.Common.Accent:Style.Common.Text;
        View.SceneryLabel.color=ShowScenery?Style.Common.Accent:Style.Common.Text;
        View.ClearGoal.gameObject.SetActive(_hasGoal);
    }
    private void ApplySceneryVisibility()
    {
        if(!IsOpen || ShowScenery) { RestoreScenery(); return; }
        foreach(var renderer in SceneryRenderers)
        {
            if(renderer==null || renderer.gameObject.scene!=gameObject.scene ||
                renderer.GetComponentInParent<TeamHJD.Game.FieldTurrets.FieldTurretBase>()!=null ||
                renderer.GetComponentInParent<PlayerInfo>()!=null || renderer.GetComponentInParent<EnemyHealth>()!=null)continue;
            if(!_hiddenScenery.ContainsKey(renderer))_hiddenScenery[renderer]=renderer.forceRenderingOff;
            renderer.forceRenderingOff=true;
        }
    }
    private void RestoreScenery()
    {
        foreach(var item in _hiddenScenery)if(item.Key!=null)item.Key.forceRenderingOff=item.Value;
        _hiddenScenery.Clear();
    }
    private void CaptureIntel()
    {
        if(_regionBinding==null || _regionBinding.Controller!=Source.Turrets)
            foreach(var binding in FindObjectsByType<RegionSceneBinding>(FindObjectsSortMode.None))
                if(binding.gameObject.scene==gameObject.scene && binding.Controller==Source.Turrets){_regionBinding=binding;break;}
        if(_composition==null)
            foreach(var root in FindObjectsByType<InitialStageCompositionRoot>(FindObjectsSortMode.None))
                if(root.gameObject.scene==gameObject.scene){_composition=root;break;}
        var battlefield=_composition!=null?_composition.TacticalBattlefield:null;
        Battlefield=battlefield!=null && battlefield.MatchId==Source.BoundMatchId?battlefield:null;
        _captureProgress.Clear();
        if(Regions!=null && _regionBinding!=null && _regionBinding.HasWriteAuthority &&
            _regionBinding.Source?.Snapshot?.GridId==Regions.GridId)
            foreach(var progress in _regionBinding.StaticCaptureProgress)_captureProgress[progress.RegionId]=progress;
        RefreshIntelLabels();
    }
    public readonly struct CaptureMarker
    {
        public readonly Vector2 Position;public readonly float Progress;public readonly bool Holding;
        public CaptureMarker(Vector2 position,float progress,bool holding){Position=position;Progress=progress;Holding=holding;}
    }
    public readonly List<CaptureMarker> CaptureMarkers=new();
    private void RefreshIntelLabels()
    {
        CaptureMarkers.Clear();if(Regions==null || _regionBinding==null)return;
        var g=Regions.Definition.Geometry;
        foreach(var p in _captureProgress.Values)
        {
            if(p.IsSecured || p.HoldSeconds<=0 && p.Reason!=StaticRegionCaptureReason.Holding)continue;
            Vector2 sum=Vector2.zero;int count=0;
            for(int y=0;y<g.CellsY;y++)for(int x=0;x<g.CellsX;x++)
                if(Regions.Definition.RegionIds[Regions.Definition.GetCellIndex(x,y)]==p.RegionId)
                {sum+=new Vector2((float)(g.MinimumX+(x+.5)*g.CellWidth),(float)(g.MinimumY+(y+.5)*g.CellHeight));count++;}
            if(count>0)CaptureMarkers.Add(new CaptureMarker(sum/count,Mathf.Clamp01((float)(p.HoldSeconds/System.Math.Max(.001,_regionBinding.CaptureSettings.HoldSeconds))),p.Reason==StaticRegionCaptureReason.Holding));
        }
    }
    private int _fieldCaptureRegion=-1;
    private float _fieldExitAt=-100,_fieldStatusChanged=-100;
    private TacticalPresentationRules.FieldState _fieldStatus;
    private void RefreshFieldStatus(bool field)
    {
        var state=TacticalPresentationRules.FieldState.Hidden;
        if(_fieldCaptureRegion>=0 && (!_captureProgress.TryGetValue(_fieldCaptureRegion,out var previous) || previous.IsSecured || previous.HoldSeconds<=0 && previous.Reason!=StaticRegionCaptureReason.Holding))_fieldCaptureRegion=-1;
        if(field && !IsOpen && !IsTransitioning && Regions!=null && !(_app!=null && _app.IsInitialized && _app.LocalInput.IsBlocked))
        {
            var position=new BattlefieldPoint(PlayerPosition.x,PlayerPosition.y);
            int region=-1;if(Regions.Definition.TryGetCellIndex(position,out int index))region=Regions.Definition.RegionIds[index];
            _captureProgress.TryGetValue(region,out var progress);
            if(_fieldCaptureRegion>=0 && region!=_fieldCaptureRegion){_fieldExitAt=Time.unscaledTime;_fieldCaptureRegion=-1;}
            if(progress!=null && !progress.IsSecured && progress.Reason==StaticRegionCaptureReason.Holding)_fieldCaptureRegion=region;
            state=TacticalPresentationRules.Field(Regions,position,progress,Time.unscaledTime-_fieldExitAt<2.4f);
        }
        if(state!=_fieldStatus)
        {
            _fieldStatus=state;_fieldStatusChanged=Time.unscaledTime;
            if(state!=TacticalPresentationRules.FieldState.Hidden)
            {
                View.FieldStatusLabel.text=TacticalPresentationRules.FieldCaption(state);
                Color tone=state==TacticalPresentationRules.FieldState.LeftCapture || state==TacticalPresentationRules.FieldState.Contested?Color.Lerp(Style.Enemy,Style.Common.Text,.55f):state==TacticalPresentationRules.FieldState.Outside || state==TacticalPresentationRules.FieldState.Locked?Style.Common.Text:Color.Lerp(Style.Common.Power,Style.Common.Text,.55f);
                View.FieldStatusLabel.color=tone;
                View.FieldStatusIcon.Set(state switch{TacticalPresentationRules.FieldState.Capturing=>TacticalSymbolGraphic.Symbol.Capturing,TacticalPresentationRules.FieldState.Paused=>TacticalSymbolGraphic.Symbol.Busy,TacticalPresentationRules.FieldState.LeftCapture=>TacticalSymbolGraphic.Symbol.RegionExit,TacticalPresentationRules.FieldState.Locked=>TacticalSymbolGraphic.Symbol.Locked,TacticalPresentationRules.FieldState.Contested=>TacticalSymbolGraphic.Symbol.Threat,_=>TacticalSymbolGraphic.Symbol.OutsideTerritory},tone);
                View.FieldStatusIcon.rectTransform.anchoredPosition=new Vector2(-View.FieldStatusLabel.preferredWidth*.5f-22,0);
            }
        }
        float target=state==TacticalPresentationRules.FieldState.Hidden?0:1;
        View.FieldStatusGroup.alpha=Mathf.MoveTowards(View.FieldStatusGroup.alpha,target,Time.unscaledDeltaTime/.14f);
        float pulse=Mathf.Sin(Mathf.Clamp01((Time.unscaledTime-_fieldStatusChanged)/.5f)*Mathf.PI);
        View.FieldStatusContent.localScale=Vector3.one*(1+pulse*.10f);
        View.FieldStatusGroup.gameObject.SetActive(target>0 || View.FieldStatusGroup.alpha>.001f);
    }
    public void SetGoal(Vector2 position)
    {
        _goal=new Vector2(Mathf.Clamp(position.x,WorldBounds.xMin,WorldBounds.xMax),Mathf.Clamp(position.y,WorldBounds.yMin,WorldBounds.yMax));
        _hasGoal=true; _goalSetAt=Time.unscaledTime; RefreshMapSettings();
    }
    public void ClearGoal() { _hasGoal=false; RefreshMapSettings(); }
    private float _goalSetAt;
}

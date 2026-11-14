// 공용 낮밤 원본과 로컬 Player의 실제 감속 상태를 읽습니다. 확정되지 않은 점유 분모/효과 시간을 만들지 않습니다.
using TeamHJD.Game.Contracts;
using TMPro;
using UnityEngine;

public sealed class CombatHudHeader : MonoBehaviour
{
    public CombatHudStyle Style;
    public PlayerSkillHud PlayerHud;
    public TMP_Text ClockLabel, OccupancyLabel, SlowCount, SlowDescription;
    public CanvasGroup SlowGroup;
    public GameObject OccupancyTitle, OccupancyDivider;
    public RectTransform HeaderBackplate;
    private bool _hasConfirmedOccupancy;
    private InitialStagePlayerHUD _regionSource;
    private float _nextRegionSearch;
    private int _lastCaptured=int.MinValue,_lastEligible=int.MinValue;
    private IMatchDayNightCycle _clock;
    private MonoBehaviour _clockObject;
    private float _nextClockSearch;
    private PlayerSkillController _owner;
    private PlayerSlowEffects _slow;
    private readonly CombatHudTransition _slowVisibility = new();
    private int _lastSeconds = int.MinValue;
    private string _lastPhase;
    private float _warningAt = float.NegativeInfinity;
    public bool IsTimeWarning { get; private set; }
    public void SetConfirmedOccupancy(int capturedEligibleCells, int eligibleCells)
    {
        bool confirmed=eligibleCells>0 && capturedEligibleCells>=0 && capturedEligibleCells<=eligibleCells;
        if(_hasConfirmedOccupancy==confirmed && _lastCaptured==capturedEligibleCells && _lastEligible==eligibleCells)return;
        _lastCaptured=capturedEligibleCells;_lastEligible=eligibleCells;_hasConfirmedOccupancy=confirmed;
        double percent=confirmed?100.0*capturedEligibleCells/eligibleCells:0;
        OccupancyLabel.text = !confirmed?"":percent>0 && percent<.1?"<0.1%":percent>0 && percent<1?
            percent.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"%":$"{Mathf.RoundToInt((float)percent)}%";
        ApplyOccupancyLayout();
    }
    private void ApplyOccupancyLayout()
    {
        OccupancyLabel.gameObject.SetActive(_hasConfirmedOccupancy);
        if(OccupancyTitle!=null)OccupancyTitle.SetActive(_hasConfirmedOccupancy);
        if(OccupancyDivider!=null)OccupancyDivider.SetActive(_hasConfirmedOccupancy);
        ClockLabel.rectTransform.anchoredPosition = new Vector2(_hasConfirmedOccupancy ? -88 : 0,-28);
        OccupancyLabel.rectTransform.sizeDelta=new Vector2(132,40);
        OccupancyLabel.rectTransform.anchoredPosition=new Vector2(174,-28);
        OccupancyLabel.enableAutoSizing=true;OccupancyLabel.fontSizeMin=20;OccupancyLabel.fontSizeMax=24;
        OccupancyLabel.textWrappingMode=TextWrappingModes.NoWrap;
        ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,_hasConfirmedOccupancy?520:300);
        if(HeaderBackplate!=null)HeaderBackplate.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,_hasConfirmedOccupancy?520:300);
    }
    private void OnEnable()
    {
        _hasConfirmedOccupancy=false;_lastCaptured=_lastEligible=int.MinValue;_regionSource=null;_nextRegionSearch=0;
        OccupancyLabel.text = ""; ClockLabel.text = "-- --:--"; ApplyOccupancyLayout();
        _lastSeconds = int.MinValue; _lastPhase = null; _warningAt = float.NegativeInfinity;
        ApplyClock(-1, "--");
    }
    private void LateUpdate()
    {
        if (_clockObject == null && Time.unscaledTime >= _nextClockSearch)
        {
            _nextClockSearch = Time.unscaledTime + .5f; _clock = null;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour.gameObject.scene == gameObject.scene && behaviour is IMatchDayNightCycle cycle)
                { _clock = cycle; _clockObject = behaviour; break; }
        }
        double remaining = _clockObject != null ? _clock.RemainingSeconds : double.NaN;
        int seconds = double.IsNaN(remaining) || double.IsInfinity(remaining) ? -1 : (int)System.Math.Min(int.MaxValue, System.Math.Ceiling(System.Math.Max(0, remaining)));
        string phase = _clockObject != null && !_clock.TimeOfDayId.IsEmpty ? _clock.Phase.ToString().ToUpperInvariant() : "--";
        ApplyClock(seconds, phase);
        if (_owner != PlayerHud.Owner)
        {
            _owner = PlayerHud.Owner; _slow = _owner != null ? _owner.GetComponent<PlayerSlowEffects>() : null;
            _regionSource=null;
            _slowVisibility.Snap(0);
        }
        RefreshConfirmedOccupancy();
        int count = _slow != null && _slow.isActiveAndEnabled ? _slow.ActiveEffectCount : 0;
        _slowVisibility.Set(count > 0 ? 1 : 0, Style.SlideSeconds);
        SlowGroup.alpha = _slowVisibility.Tick(Time.unscaledDeltaTime); SlowGroup.blocksRaycasts = count > 0;
        SlowCount.text = count > 1 ? count.ToString() : "";
        SlowDescription.text = count > 0 ? $"이동 속도 {Mathf.RoundToInt(_slow.SpeedMultiplier * 100)}%" : "";
    }
    private void RefreshConfirmedOccupancy()
    {
        if(_regionSource==null && _owner!=null && Time.unscaledTime>=_nextRegionSearch)
        {
            _nextRegionSearch=Time.unscaledTime+.5f;
            foreach(var source in FindObjectsByType<InitialStagePlayerHUD>(FindObjectsSortMode.None))
                if(source.gameObject.scene==gameObject.scene && source.LocalPlayer!=null && source.LocalPlayer.gameObject==_owner.gameObject)
                {_regionSource=source;break;}
        }
        var tactical=_regionSource!=null && _regionSource.isActiveAndEnabled?_regionSource.Tactical:null;
        var regions=tactical!=null && tactical.isActiveAndEnabled?tactical.Regions:null;
        if(regions!=null && (!_regionSource.RequiresCommands || regions.MatchId==_regionSource.BoundMatchId))
            SetConfirmedOccupancy(regions.CapturedCellCount,regions.Definition.CapturableCellCount);
        else SetConfirmedOccupancy(-1,0);
    }
    private void ApplyClock(int seconds, string phase)
    {
        IsTimeWarning = seconds >= 0 && seconds <= 5;
        if (_lastSeconds != seconds || _lastPhase != phase)
        {
            _lastSeconds = seconds; _lastPhase = phase;
            ClockLabel.text = seconds >= 0 ? $"{phase} {seconds / 60:00}:{seconds % 60:00}" : $"{phase} --:--";
            _warningAt = IsTimeWarning ? Time.unscaledTime : float.NegativeInfinity;
        }
        ClockLabel.color = IsTimeWarning ? Style.Health : Style.Text;
        float pulse = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - _warningAt) / .32f));
        ClockLabel.rectTransform.localScale = Vector3.one * (1 + (IsTimeWarning ? .12f * pulse : 0));
    }
    private void OnDisable() { _owner = null; _slow = null; _clock = null; _clockObject = null; _regionSource=null; }
}

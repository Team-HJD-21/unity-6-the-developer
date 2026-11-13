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
        OccupancyLabel.text = eligibleCells <= 0 || capturedEligibleCells < 0 || capturedEligibleCells > eligibleCells ? "--%" :
            $"{Mathf.RoundToInt(100f * capturedEligibleCells / eligibleCells)}%";
    }
    private void OnEnable()
    {
        OccupancyLabel.text = "--%"; ClockLabel.text = "-- --:--";
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
            _slowVisibility.Snap(0);
        }
        int count = _slow != null && _slow.isActiveAndEnabled ? _slow.ActiveEffectCount : 0;
        _slowVisibility.Set(count > 0 ? 1 : 0, Style.SlideSeconds);
        SlowGroup.alpha = _slowVisibility.Tick(Time.unscaledDeltaTime); SlowGroup.blocksRaycasts = count > 0;
        SlowCount.text = count > 1 ? count.ToString() : "";
        SlowDescription.text = count > 0 ? $"이동 속도 {Mathf.RoundToInt(_slow.SpeedMultiplier * 100)}%" : "";
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
    private void OnDisable() { _owner = null; _slow = null; _clock = null; _clockObject = null; }
}

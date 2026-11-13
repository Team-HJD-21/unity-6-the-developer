// Player와 CU의 독립 자동/고정 표시를 처리합니다. CU의 두 바는 같은 이 인스턴스에 속합니다.
using TMPro;
using UnityEngine;

public sealed class CombatVitalsGroup : MonoBehaviour
{
    public CombatHudStyle Style;
    public RectTransform Slide;
    public CanvasGroup Group;
    public CombatVitalsBar Primary, Secondary;
    public TMP_Text Details;
    public GameObject Pin;
    public bool RightEdge;
    public bool StartPinned = true;
    public bool IsPinned { get; private set; }
    public float Visibility => _motion.Value;
    public bool HasSource => Primary.Maximum > 0 || (Secondary != null && Secondary.Maximum > 0);
    private readonly CombatHudTransition _motion = new();
    private float _visibleUntil;
    private CombatVitalsBar _hover;
    private bool _contextVisible = true;
    private float _hitAt = float.NegativeInfinity;
    public void NotifyDamage() { if (_contextVisible) _hitAt = Time.unscaledTime; }

    public void SetContextVisible(bool visible)
    {
        if (_contextVisible == visible) return;
        _contextVisible = visible;
        if (!visible)
        {
            _hover = null; _motion.Snap(0); Apply(0);
            Pin.SetActive(false); Details.enabled = false;
        }
    }

    private void Awake() { IsPinned = StartPinned; _motion.Snap(0); Apply(0); }
    public void SetPrimary(int current, int maximum) => Set(Primary, current, maximum);
    public void SetSecondary(int current, int maximum) { if (Secondary != null) Set(Secondary, current, maximum); }
    private void Set(CombatVitalsBar bar, int current, int maximum)
    {
        if (bar.Current != current || bar.Maximum != maximum) _visibleUntil = Time.unscaledTime + Style.AutoHideSeconds;
        bar.Set(current, maximum);
    }
    public void Hover(CombatVitalsBar bar, bool over)
    {
        if (over) _hover = bar;
        else if (_hover == bar) _hover = null;
    }
    public void TogglePin()
    {
        if (!HasSource) return;
        IsPinned = !IsPinned;
        _visibleUntil = Time.unscaledTime + Style.AutoHideSeconds;
    }
    public void Clear()
    {
        IsPinned = StartPinned; _hover = null; _visibleUntil = 0;
        _hitAt = float.NegativeInfinity;
        Primary.Set(0, 0); if (Secondary != null) Secondary.Set(0, 0);
        _motion.Snap(0); Apply(0);
    }
    private void LateUpdate()
    {
        bool edge = RightEdge ? GameInput.PointerPosition.x >= Screen.width - Screen.width * .052f : GameInput.PointerPosition.x <= Screen.width * .052f;
        _motion.Set(_contextVisible && HasSource && (IsPinned || edge || _hover != null || Time.unscaledTime < _visibleUntil) ? 1 : 0, Style.SlideSeconds);
        Apply(_motion.Tick(Time.unscaledDeltaTime));
        Pin.SetActive(_contextVisible && HasSource && IsPinned);
        Details.enabled = _hover != null && _hover.Maximum > 0 && _motion.Value > .1f;
        if (Details.enabled) Details.text = $"{_hover.Current} / {_hover.Maximum}";
    }
    private void Apply(float progress)
    {
        float elapsed = Time.unscaledTime - _hitAt;
        float shake = 1 - Mathf.SmoothStep(0, 1, elapsed / .32f);
        Vector2 offset = shake > 0
            ? new Vector2(Mathf.Sin(elapsed * 81) * 5 * shake, Mathf.Sin(elapsed * 57) * 2.5f * shake)
            : Vector2.zero;
        Slide.anchoredPosition = new Vector2((RightEdge ? 120 : -120) * (1 - progress), 0) + offset;
        Group.alpha = progress;
        Group.blocksRaycasts = progress > .05f;
    }
    private void OnDisable() => Clear();
}

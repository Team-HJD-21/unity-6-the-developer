// 현재 무기 한 장과 작은 번호 목록만 표시합니다. 교체 시 해당 카드의 짧은 회전은 유지합니다.
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(500)]
public sealed class CombatWeaponStack : MonoBehaviour
{
    public CombatHudStyle Style;
    public PlayerWeaponHud Hud;
    public PlayerWeaponSlotView[] Slots;
    public CanvasGroup[] Groups;
    public Button[] NumberButtons;
    public TMP_Text[] NumberLabels;
    public TMP_Text[] NumberShadows;
    public PlayerSkillHud SkillHud;
    public RectTransform PanelRect;
    public Image PanelEdge;
    public Vector2 CardPosition = new(50, 64), NamePosition = new(175, 35);
    public CanvasGroup NameGroup;
    public RectTransform NameRect;
    public TMP_Text NameLabel;
    public string[] WeaponNames = { "스패너", "기본총", "냉각총", "스나이퍼" };
    [Min(0)] public float NameHoldSeconds = 1.4f;
    [Min(.01f)] public float NameFadeSeconds = .18f;
    private UnityAction[] _choose;
    private PlayerWeaponSelection _owner;
    private float _changeAt;
    private int _selected = -1;
    private float _nameUntil, _nameAlpha, _nameFrom, _nameTarget, _nameElapsed;
    public int SelectedIndex => _selected;
    private void OnEnable()
    {
        if (NumberButtons == null) return;
        _choose = new UnityAction[NumberButtons.Length];
        for (int i = 0; i < NumberButtons.Length; i++)
        {
            int slot = i + 1;
            int index = i;
            _choose[i] = () =>
            {
                if (Hud.Owner != null) Hud.Owner.TrySelectWeaponSlot(slot);
                // A world HUD click must not retain Submit focus and turn the next SPACE dash into another selection.
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == NumberButtons[index].gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            };
            NumberButtons[i].onClick.AddListener(_choose[i]);
        }
    }
    private void LateUpdate()
    {
        if (_owner != Hud.Owner)
        {
            if (_owner != null) _owner.WeaponChanged -= Changed;
            _owner = Hud.Owner; _selected = -1;
            _nameUntil = 0;
            if (_owner != null) { _owner.WeaponChanged += Changed; Changed(_owner.CurrentWeapon); }
        }
        float t = Mathf.Clamp01((Time.unscaledTime - _changeAt) / Style.SlideSeconds);
        float eased = CombatHudMotion.Sigmoid(t);
        for (int index = 0; index < Slots.Length; index++)
        {
            bool selected = index == _selected;
            var rect = (RectTransform)Slots[index].transform;
            rect.anchoredPosition = CardPosition + Vector2.up * (selected ? 8 * (1 - eased) : 0);
            rect.localRotation = Quaternion.Euler(0, 0, selected ? Mathf.Sin(t * Mathf.PI) * 5 : 0);
            Groups[index].alpha = selected ? 1 : 0;
            Groups[index].blocksRaycasts = Groups[index].interactable = selected;
            if (NumberLabels == null || index >= NumberLabels.Length) continue;
            bool available = Slots[index].GetComponent<Button>().interactable;
            NumberButtons[index].interactable = available;
            NumberLabels[index].fontSize = selected ? 25 : 17;
            NumberLabels[index].color = selected ? Style.Accent : new Color(.72f, .75f, .7f, 1);
            if (!available) NumberLabels[index].color = new Color(.26f, .29f, .28f, 1);
            if (NumberShadows != null && index < NumberShadows.Length && NumberShadows[index] != null)
                NumberShadows[index].fontSize = NumberLabels[index].fontSize;
        }
        UpdateName();
    }
    private void UpdateName()
    {
        if (NameGroup == null) return;
        bool usable = _owner != null && _owner.CanSelectWeapon && SkillHud != null && SkillHud.AreCombatControlsVisible;
        int hovered = -1;
        if (usable)
        {
            if (PanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(PanelRect, GameInput.PointerPosition)) hovered = _selected;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!NumberButtons[i].interactable) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)NumberButtons[i].transform, GameInput.PointerPosition) ||
                    (i == _selected && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)Slots[i].transform, GameInput.PointerPosition)))
                    hovered = i;
            }
        }
        if (PanelEdge != null) PanelEdge.color = hovered >= 0 ? new Color(.83f, .8f, .65f, .9f) : new Color(.57f, .63f, .59f, .7f);
        float target = usable && (hovered >= 0 || Time.unscaledTime < _nameUntil) ? 1 : 0;
        if (_nameTarget != target)
        { _nameTarget = target; _nameFrom = _nameAlpha; _nameElapsed = 0; }
        _nameElapsed += Time.unscaledDeltaTime;
        _nameAlpha = Mathf.Lerp(_nameFrom, _nameTarget, Mathf.SmoothStep(0, 1, _nameElapsed / NameFadeSeconds));
        NameGroup.alpha = _nameAlpha;
        NameRect.anchoredPosition = NamePosition + Vector2.down * (4 * (1 - _nameAlpha));
        int shown = hovered >= 0 ? hovered : _selected;
        if (shown >= 0 && shown < WeaponNames.Length) NameLabel.text = WeaponNames[shown];
    }
    private void Changed(WeaponType weapon)
    {
        int selected = -1;
        for (int i = 0; i < Slots.Length; i++) if (Slots[i].Weapon == weapon) selected = i;
        if (selected < 0 || selected == _selected) return;
        bool first = _selected < 0; _selected = selected;
        Slots[selected].transform.SetAsLastSibling();
        // Keep the separate number rail above the weapon's hit surface during card rotation.
        if (NumberButtons != null && NumberButtons.Length > 0) NumberButtons[0].transform.parent.SetAsLastSibling();
        _changeAt = Time.unscaledTime - (first ? Style.SlideSeconds : 0);
        if (!first) _nameUntil = Time.unscaledTime + NameHoldSeconds;
    }
    private void OnDisable()
    {
        if (_owner != null) _owner.WeaponChanged -= Changed;
        _owner = null; _selected = -1;
        if (_choose != null)
            for (int i = 0; i < _choose.Length; i++) NumberButtons[i].onClick.RemoveListener(_choose[i]);
        _choose = null;
        _nameUntil = _nameAlpha = _nameFrom = _nameTarget = _nameElapsed = 0;
        if (NameGroup != null) NameGroup.alpha = 0;
    }
}

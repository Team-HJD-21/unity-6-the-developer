using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static PlayerSkillUiFactory;

/// <summary>스킬 툴팁과 화면 안의 표시 위치를 관리합니다.</summary>
public sealed class PlayerSkillTooltipView
{
    private readonly RectTransform _canvas;
    private readonly PlayerSkillController _owner;
    private readonly PlayerSkillUiFactory _ui;
    private readonly Func<bool> _isOpen;
    private RectTransform _tooltip;
    private TMP_Text _tooltipTitle, _tooltipDescription;
    private Func<PlayerSkillId> _hoveredSkill;
    public PlayerSkillTooltipView(RectTransform canvas, PlayerSkillController owner, PlayerSkillUiFactory ui, Func<bool> isOpen)
    { _canvas = canvas; _owner = owner; _ui = ui; _isOpen = isOpen; CreateTooltip(); }
    private void CreateTooltip()
    {
        _tooltip = Rect("Skill Tooltip", _canvas, new Vector2(460, 230));
        _tooltip.pivot = new Vector2(0, 1);
        Box(_tooltip, Cyan);
        Box(Rect("Tooltip Background", _tooltip, new Vector2(456, 226)), Dark);
        // 자식은 부모 피벗과 관계없이 중앙을 기준으로 배치됩니다.
        _tooltipTitle = _ui.Label("", _tooltip, new Vector2(420, 34), new Vector2(0, 83), 24, Cyan);
        _tooltipDescription = _ui.Label("", _tooltip, new Vector2(420, 160), new Vector2(0, -20), 19, Color.white);
        _tooltipDescription.alignment = TextAlignmentOptions.Left;
        _tooltip.gameObject.SetActive(false);
    }
    public void AddHover(RectTransform target, System.Func<PlayerSkillId> skill)
    {
        var trigger = target.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { _hoveredSkill = skill; Refresh(); });
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => { if (_hoveredSkill == skill) Hide(); });
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }
    public void Refresh()
    {
        if (_tooltip == null) return;
        var skill = _hoveredSkill != null ? _hoveredSkill() : PlayerSkillId.None;
        if (skill == PlayerSkillId.None || (!_isOpen() && !_owner.CanPrepare && !_owner.CanUse))
        {
            _tooltip.gameObject.SetActive(false);
            return;
        }
        _tooltip.gameObject.SetActive(true);
        _tooltip.SetAsLastSibling();
        _tooltipTitle.text = PlayerSkillDescriptions.Name(skill);
        string key = _owner.Loadout.Q == skill ? "Q" : _owner.Loadout.E == skill ? "E" : "";
        string hint = key.Length > 0 ? key + " · " : "";
        _tooltipDescription.text = hint + PlayerSkillDescriptions.Description(skill, _owner);
        var canvasRect = _canvas;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, GameInput.PointerPosition, null, out var point)) return;
        var bounds = canvasRect.rect;
        var size = _tooltip.sizeDelta;
        _tooltip.anchoredPosition = new Vector2(
            Mathf.Clamp(point.x + 18, bounds.xMin + 8, Mathf.Max(bounds.xMin + 8, bounds.xMax - size.x - 8)),
            Mathf.Clamp(point.y - 18, Mathf.Min(bounds.yMax - 8, bounds.yMin + size.y + 8), bounds.yMax - 8));
    }
    public void Hide()
    {
        _hoveredSkill = null;
        if (_tooltip != null) _tooltip.gameObject.SetActive(false);
    }
}

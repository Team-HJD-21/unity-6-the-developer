using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using static PlayerSkillUiFactory;

/// <summary>스킬 선택 카드와 Q/E 미리보기를 관리합니다.</summary>
public sealed class PlayerSkillSelectionPanelView
{
    private static readonly PlayerSkillId[] Skills = { PlayerSkillId.Bomb, PlayerSkillId.TemporaryTurret, PlayerSkillId.EnergyBarrier, PlayerSkillId.LureBeacon };
    private readonly Transform _canvas;
    private readonly PlayerSkillController _owner;
    private readonly PlayerSkillUiFactory _ui;
    private readonly PlayerSkillTooltipView _tooltip;
    private readonly Func<PlayerSkillId, Sprite> _icon;
    private readonly Func<int, Transform, PlayerSkillSlotView> _createSlot;
    private readonly UnityAction _close;
    private readonly Action<PlayerSkillId> _select;
    private GameObject _modal;
    private TMP_Text _instruction;
    private readonly PlayerSkillSlotView[] _selectionSlots = new PlayerSkillSlotView[2];
    private readonly TMP_Text[] _selectionLabels = new TMP_Text[2];
    private readonly Button[] _cards = new Button[4];
    private readonly TMP_Text[] _cardLabels = new TMP_Text[4];
    private Button _reset;
    public bool IsOpen => _modal.activeSelf;
    public void SetOpen(bool open) => _modal.SetActive(open);
    public PlayerSkillSelectionPanelView(Transform canvas, PlayerSkillController owner, PlayerSkillUiFactory ui, PlayerSkillTooltipView tooltip, Func<PlayerSkillId, Sprite> icon, Func<int, Transform, PlayerSkillSlotView> createSlot, UnityAction close, Action<PlayerSkillId> select)
    { _canvas = canvas; _owner = owner; _ui = ui; _tooltip = tooltip; _icon = icon; _createSlot = createSlot; _close = close; _select = select; CreateModal(); }
    private void CreateModal()
    {
        var backdrop = Rect("Skill Selection", _canvas, Vector2.zero);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        var dim = Box(backdrop, new Color(0, 0, 0, .66f));
        dim.raycastTarget = true;
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(_close);
        _modal = backdrop.gameObject;
        var border = Rect("Panel Border", backdrop, new Vector2(1280, 740));
        Box(border, Cyan);
        var panel = Rect("Skill Selection Panel", border, new Vector2(1274, 734));
        Box(panel, Dark).raycastTarget = true;
        panel.gameObject.AddComponent<Button>(); // 패널의 빈 공간 클릭이 배경 닫기로 전달되지 않게 합니다.
        _ui.Label("스킬 선택", panel, new Vector2(1100, 44), new Vector2(0, 320), 32, Color.white);
        _instruction = _ui.Label("", panel, new Vector2(1100, 36), new Vector2(0, 275), 22, Cyan);
        for (int i = 0; i < 8; i++)
        {
            bool implemented = i < Skills.Length;
            PlayerSkillId skill = implemented ? Skills[i] : PlayerSkillId.None;
            var card = Rect(implemented ? skill.ToString() : "Development Slot " + (i + 1), panel, new Vector2(264, 160));
            card.anchoredPosition = new Vector2(-444 + (i % 4) * 296, i < 4 ? 175 : -10);
            Box(card, implemented ? Cyan : new Color(.18f, .28f, .31f)).raycastTarget = true;
            Box(Rect("Card Background", card, new Vector2(260, 156)), new Color(.075f, .15f, .19f));
            if (!implemented)
            {
                _ui.Label("개발 중", card, new Vector2(240, 44), Vector2.zero, 26, new Color(.45f, .55f, .58f));
                continue;
            }
            _cards[i] = card.gameObject.AddComponent<Button>();
            _cards[i].onClick.AddListener(() => _select(skill));
            _tooltip.AddHover(card, () => skill);
            var icon = Box(Rect("Icon", card, new Vector2(76, 76)), Color.white);
            icon.rectTransform.anchoredPosition = new Vector2(0, 30);
            icon.sprite = _icon(skill);
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;
            if (icon.sprite == null && (skill == PlayerSkillId.EnergyBarrier || skill == PlayerSkillId.LureBeacon))
                CreateDeviceIcon(icon.rectTransform, skill);
            _ui.Label(PlayerSkillDescriptions.Name(skill), card, new Vector2(240, 30), new Vector2(0, -26), 24, Color.white);
            _cardLabels[i] = _ui.Label("선택", card, new Vector2(240, 28), new Vector2(0, -58), 19, Cyan);
        }
        _ui.Label("선택한 스킬", panel, new Vector2(400, 30), new Vector2(0, -125), 22, Cyan);
        for (int i = 0; i < _selectionSlots.Length; i++)
        {
            _selectionSlots[i] = _createSlot(i, panel);
            _selectionLabels[i] = _ui.Label("미선택", panel, new Vector2(250, 28), new Vector2(i == 0 ? -148 : 148, -286), 20, Color.white);
        }
        _reset = _ui.TextButton("선택 초기화", panel, new Vector2(-290, -329), _owner.ClearSelection);
        _ui.TextButton("닫기", panel, new Vector2(290, -329), _close);
        _modal.SetActive(false);
    }
    public void Refresh()
    {
        for (int i = 0; i < _selectionSlots.Length; i++)
        {
            var skill = i == 0 ? _owner.Loadout.Q : _owner.Loadout.E;
            _selectionSlots[i].Refresh(skill);
            _selectionLabels[i].text = skill == PlayerSkillId.None ? "미선택" : PlayerSkillDescriptions.Name(skill);
        }
        if (_instruction == null) return;
        bool full = _owner.Loadout.E != PlayerSkillId.None;
        _instruction.text = full ? "Q / E 등록 완료 · 변경하려면 선택을 초기화하세요" :
            _owner.Loadout.Q == PlayerSkillId.None ? "먼저 선택한 스킬 → Q   /   다음 스킬 → E" : "Q 등록 완료 · 다음 스킬은 E에 등록됩니다";
        for (int i = 0; i < Skills.Length; i++)
        {
            var skill = Skills[i];
            bool chosen = _owner.Loadout.Contains(skill);
            _cards[i].interactable = _owner.CanPrepare && !full && !chosen && _owner.IsAvailable(skill);
            _cardLabels[i].text = chosen ? (_owner.Loadout.Q == skill ? "Q 등록됨" : "E 등록됨") :
                !_owner.IsAvailable(skill) ? "준비되지 않음" : "선택";
        }
        _reset.interactable = _owner.CanPrepare && _owner.Loadout.Q != PlayerSkillId.None;
    }

}

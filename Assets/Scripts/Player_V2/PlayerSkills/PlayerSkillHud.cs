// Player 스킬의 빈 슬롯, 준비 중 선택 패널, 선택된 아이콘과 쿨타임을 표시하는 uGUI View입니다.
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class PlayerSkillHud : MonoBehaviour
{
    private static readonly Color Cyan = new Color(.35f, .95f, 1f);
    private static readonly Color Dark = new Color(.035f, .09f, .12f, .97f);
    private static readonly PlayerSkillId[] Skills = { PlayerSkillId.Bomb, PlayerSkillId.TemporaryTurret, PlayerSkillId.EnergyBarrier, PlayerSkillId.LureBeacon };
    private PlayerSkillController _owner;
    private Sprite _bombIcon, _turretIcon, _barrierIcon, _beaconIcon;
    private TMP_FontAsset _font;
    private GameObject _modal;
    private TMP_Text _instruction;
    private readonly Slot[] _slots = new Slot[2];
    private readonly Slot[] _selectionSlots = new Slot[2];
    private readonly TMP_Text[] _selectionLabels = new TMP_Text[2];
    private readonly Button[] _cards = new Button[4];
    private readonly TMP_Text[] _cardLabels = new TMP_Text[4];
    private Button _reset;
    private Sprite _cooldownMaskSprite;
    private RectTransform _recoveryPrompt;
    private Camera _worldCamera;
    private RectTransform _tooltip;
    private TMP_Text _tooltipTitle, _tooltipDescription;
    private TMP_Text _placementHint;
    private System.Func<PlayerSkillId> _hoveredSkill;
    public bool IsOpen => _modal != null && _modal.activeSelf && gameObject.activeInHierarchy;

    private sealed class Slot
    {
        public Button Button;
        public Image Border, Icon, Cooldown;
        public PlayerFieldSkillIcon DeviceIcon;
        public GameObject Empty;
        public TMP_Text Status;
    }

    public void Bind(PlayerSkillController owner, Sprite bombIcon, Sprite turretIcon, TMP_FontAsset font,
        Sprite barrierIcon = null, Sprite beaconIcon = null, Sprite recoveryKeyIcon = null)
    {
        _owner = owner;
        _bombIcon = bombIcon;
        _turretIcon = turretIcon;
        _barrierIcon = barrierIcon;
        _beaconIcon = beaconIcon;
        _font = font != null ? font : TMP_Settings.defaultFontAsset;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _cooldownMaskSprite = Sprite.Create(Texture2D.whiteTexture,
            new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f);
        if (EventSystem.current == null)
        {
            var events = new GameObject("Skill UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }
        for (int i = 0; i < _slots.Length; i++) _slots[i] = CreateSlot(i);
        CreateRecoveryPrompt(recoveryKeyIcon);
        _placementHint = Label("", transform, new Vector2(1050, 40), new Vector2(0, -380), 24, Cyan);
        CreateModal();
        CreateTooltip();
        _owner.Loadout.Changed += Refresh;
        Refresh();
    }

    private Slot CreateSlot(int index, Transform parent = null)
    {
        bool preview = parent != null;
        var root = Rect(index == 0 ? "Q Skill Slot" : "E Skill Slot", parent != null ? parent : transform, new Vector2(88, 88));
        if (preview)
        {
            root.anchoredPosition = new Vector2(index == 0 ? -148 : 148, -205);
            root.localScale = Vector3.one * 1.5f;
        }
        else
        {
            root.anchorMin = root.anchorMax = new Vector2(0, 1);
            root.pivot = new Vector2(0, 1);
            root.anchoredPosition = new Vector2(40, -180 - index * 104);
        }
        var slot = new Slot { Border = Box(root, new Color(.22f, .39f, .43f)) };
        slot.Button = root.gameObject.AddComponent<Button>();
        if (!preview) slot.Button.onClick.AddListener(Open);
        AddHover(root, () => index == 0 ? _owner.Loadout.Q : _owner.Loadout.E);
        var inner = Rect("Background", root, new Vector2(80, 80));
        Box(inner, Dark);
        var empty = Rect("Empty Grid", inner, new Vector2(72, 72));
        slot.Empty = empty.gameObject;
        for (int i = -1; i <= 1; i++)
        {
            var vertical = Rect("Grid Column", empty, new Vector2(1, 70));
            vertical.anchoredPosition = new Vector2(i * 22, 0);
            Box(vertical, new Color(.22f, .4f, .43f, .5f));
            var horizontal = Rect("Grid Row", empty, new Vector2(70, 1));
            horizontal.anchoredPosition = new Vector2(0, i * 22);
            Box(horizontal, new Color(.22f, .4f, .43f, .5f));
        }
        Label("+", empty, new Vector2(65, 65), Vector2.zero, 48, new Color(.65f, .78f, .8f));
        var iconRoot = Rect("Skill Icon", inner, new Vector2(66, 66));
        slot.Icon = Box(iconRoot, Color.white);
        slot.Icon.preserveAspect = true;
        slot.DeviceIcon = CreateDeviceIcon(iconRoot, PlayerSkillId.None);
        slot.Cooldown = Box(Rect("Cooldown Sweep", inner, new Vector2(66, 66)), new Color(.28f, .3f, .32f, .85f));
        slot.Cooldown.sprite = _cooldownMaskSprite;
        slot.Cooldown.type = Image.Type.Filled;
        slot.Cooldown.fillMethod = Image.FillMethod.Radial360;
        slot.Cooldown.fillOrigin = (int)Image.Origin360.Top;
        // 남은 회색 영역은 반시계방향으로 채워, 사라지는 영역이 12시부터 시계방향으로 진행합니다.
        slot.Cooldown.fillClockwise = false;
        Label(index == 0 ? "Q" : "E", root, new Vector2(25, 25), new Vector2(30, -30), 22, Color.white);
        slot.Status = Label("", root, new Vector2(62, 26), new Vector2(-6, -28), 19, Color.white);
        return slot;
    }

    private void CreateModal()
    {
        var backdrop = Rect("Skill Selection", transform, Vector2.zero);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        var dim = Box(backdrop, new Color(0, 0, 0, .66f));
        dim.raycastTarget = true;
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        _modal = backdrop.gameObject;
        var border = Rect("Panel Border", backdrop, new Vector2(1280, 740));
        Box(border, Cyan);
        var panel = Rect("Skill Selection Panel", border, new Vector2(1274, 734));
        Box(panel, Dark).raycastTarget = true;
        panel.gameObject.AddComponent<Button>(); // 패널의 빈 공간 클릭이 배경 닫기로 전달되지 않게 합니다.
        Label("스킬 선택", panel, new Vector2(1100, 44), new Vector2(0, 320), 32, Color.white);
        _instruction = Label("", panel, new Vector2(1100, 36), new Vector2(0, 275), 22, Cyan);
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
                Label("개발 중", card, new Vector2(240, 44), Vector2.zero, 26, new Color(.45f, .55f, .58f));
                continue;
            }
            _cards[i] = card.gameObject.AddComponent<Button>();
            _cards[i].onClick.AddListener(() => Select(skill));
            AddHover(card, () => skill);
            var icon = Box(Rect("Icon", card, new Vector2(76, 76)), Color.white);
            icon.rectTransform.anchoredPosition = new Vector2(0, 30);
            icon.sprite = Icon(skill);
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;
            if (icon.sprite == null && (skill == PlayerSkillId.EnergyBarrier || skill == PlayerSkillId.LureBeacon))
                CreateDeviceIcon(icon.rectTransform, skill);
            Label(SkillName(skill), card, new Vector2(240, 30), new Vector2(0, -26), 24, Color.white);
            _cardLabels[i] = Label("선택", card, new Vector2(240, 28), new Vector2(0, -58), 19, Cyan);
        }
        Label("선택한 스킬", panel, new Vector2(400, 30), new Vector2(0, -125), 22, Cyan);
        for (int i = 0; i < _selectionSlots.Length; i++)
        {
            _selectionSlots[i] = CreateSlot(i, panel);
            _selectionLabels[i] = Label("미선택", panel, new Vector2(250, 28), new Vector2(i == 0 ? -148 : 148, -286), 20, Color.white);
        }
        _reset = TextButton("선택 초기화", panel, new Vector2(-290, -329), _owner.ClearSelection);
        TextButton("닫기", panel, new Vector2(290, -329), Close);
        _modal.SetActive(false);
    }

    public void Open()
    {
        if (!_owner.CanPrepare) return;
        HideTooltip();
        _modal.SetActive(true);
        Refresh();
        RefreshRecoveryPrompt();
    }

    private void CreateRecoveryPrompt(Sprite keySprite)
    {
        _recoveryPrompt = Rect("Turret Recovery Prompt", transform, new Vector2(230, 60));
        Box(_recoveryPrompt, Cyan);
        Box(Rect("Recovery Background", _recoveryPrompt, new Vector2(226, 56)), Dark);
        var keyRect = Rect("F Key Icon", _recoveryPrompt, new Vector2(48, 48));
        keyRect.anchoredPosition = new Vector2(-82, 0);
        if (keySprite != null)
        {
            var image = Box(keyRect, Color.white);
            image.sprite = keySprite;
            image.preserveAspect = true;
        }
        else Label("F", keyRect, new Vector2(48, 48), Vector2.zero, 32, Color.white);
        Label("포탑 회수", _recoveryPrompt, new Vector2(150, 44), new Vector2(27, 0), 26, Color.white);
        _recoveryPrompt.gameObject.SetActive(false);
    }

    private void RefreshRecoveryPrompt()
    {
        Transform turret = _owner.RecoverableTurret;
        if (_worldCamera == null) _worldCamera = Camera.main;
        bool visible = turret != null && _worldCamera != null && !IsOpen;
        _recoveryPrompt.gameObject.SetActive(visible);
        if (!visible) return;
        var baseRenderer = turret.GetComponent<SpriteRenderer>();
        Vector3 bottom = turret.position;
        if (baseRenderer != null) bottom.y = baseRenderer.bounds.min.y;
        Vector3 screen = _worldCamera.WorldToScreenPoint(bottom);
        if (screen.z <= 0) { _recoveryPrompt.gameObject.SetActive(false); return; }
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
            screen, null, out Vector2 point);
        _recoveryPrompt.anchoredPosition = point + Vector2.down * 38;
    }

    public void Close()
    {
        HideTooltip();
        if (_modal != null) _modal.SetActive(false);
    }

    private void Select(PlayerSkillId skill)
    {
        if (!_owner.IsAvailable(skill) || !_owner.TrySelect(skill)) return;
        if (_owner.Loadout.E != PlayerSkillId.None) Close();
    }

    private void LateUpdate()
    {
        if (_owner == null) return;
        if (IsOpen && (!_owner.CanPrepare || GameInput.WasPressedThisFrame(GameKey.Escape))) Close();
        Refresh();
        RefreshRecoveryPrompt();
        RefreshTooltip();
        if (_placementHint != null)
        {
            _placementHint.text = _owner.BarrierPlacementHint;
            _placementHint.gameObject.SetActive(!IsOpen && _placementHint.text.Length > 0);
        }
    }

    private void Refresh()
    {
        if (_owner == null) return;
        for (int i = 0; i < _slots.Length; i++)
        {
            var skill = i == 0 ? _owner.Loadout.Q : _owner.Loadout.E;
            RefreshSlot(_slots[i], skill);
            RefreshSlot(_selectionSlots[i], skill);
            if (_selectionLabels[i] != null) _selectionLabels[i].text = skill == PlayerSkillId.None ? "미선택" : SkillName(skill);
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

    private void RefreshSlot(Slot slot, PlayerSkillId skill)
    {
        if (slot == null) return;
        bool assigned = skill != PlayerSkillId.None;
        slot.Empty.SetActive(!assigned);
        slot.Icon.sprite = Icon(skill);
        slot.Icon.enabled = assigned && slot.Icon.sprite != null;
        bool device = skill == PlayerSkillId.EnergyBarrier || skill == PlayerSkillId.LureBeacon;
        slot.DeviceIcon.SetSkill(skill);
        slot.DeviceIcon.enabled = device && slot.Icon.sprite == null;
        float remaining = _owner.RemainingCooldown(skill);
        int count = _owner.SkillCount(skill);
        bool ready = assigned && _owner.IsSkillReady(skill);
        slot.Border.color = assigned ? (ready ? Cyan : new Color(.2f, .5f, .55f)) : new Color(.22f, .39f, .43f);
        slot.Icon.color = count > 0 ? Color.white : new Color(.4f, .5f, .55f);
        slot.DeviceIcon.color = slot.Icon.color;
        slot.Cooldown.enabled = assigned && remaining > 0;
        slot.Cooldown.color = new Color(.28f, .3f, .32f, skill == PlayerSkillId.EnergyBarrier && count > 0 ? .35f : .85f);
        slot.Cooldown.fillAmount = _owner.CooldownProgress(skill);
        slot.Status.text = assigned ? count.ToString() : "";
        // 전투 중에도 툴팁을 표시하되 대화/일시정지 UI는 가로채지 않습니다.
        slot.Border.raycastTarget = _owner.CanPrepare || _owner.CanUse;
    }

    private static string SkillName(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => "시한폭탄",
        PlayerSkillId.TemporaryTurret => "보조 포탑",
        PlayerSkillId.EnergyBarrier => "방벽 투사기",
        PlayerSkillId.LureBeacon => "유도 비콘",
        _ => "미선택"
    };

    private static PlayerFieldSkillIcon CreateDeviceIcon(RectTransform parent, PlayerSkillId skill)
    {
        var rect = Rect("Device Icon", parent, parent.sizeDelta);
        var icon = rect.gameObject.AddComponent<PlayerFieldSkillIcon>();
        icon.raycastTarget = false;
        icon.SetSkill(skill);
        icon.enabled = skill == PlayerSkillId.EnergyBarrier || skill == PlayerSkillId.LureBeacon;
        return icon;
    }

    private void CreateTooltip()
    {
        _tooltip = Rect("Skill Tooltip", transform, new Vector2(460, 230));
        _tooltip.pivot = new Vector2(0, 1);
        Box(_tooltip, Cyan);
        Box(Rect("Tooltip Background", _tooltip, new Vector2(456, 226)), Dark);
        // 자식은 부모 피벗과 관계없이 중앙을 기준으로 배치됩니다.
        _tooltipTitle = Label("", _tooltip, new Vector2(420, 34), new Vector2(0, 83), 24, Cyan);
        _tooltipDescription = Label("", _tooltip, new Vector2(420, 160), new Vector2(0, -20), 19, Color.white);
        _tooltipDescription.alignment = TextAlignmentOptions.Left;
        _tooltip.gameObject.SetActive(false);
    }

    private void AddHover(RectTransform target, System.Func<PlayerSkillId> skill)
    {
        var trigger = target.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { _hoveredSkill = skill; RefreshTooltip(); });
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => { if (_hoveredSkill == skill) HideTooltip(); });
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    private void RefreshTooltip()
    {
        if (_tooltip == null) return;
        var skill = _hoveredSkill != null ? _hoveredSkill() : PlayerSkillId.None;
        if (skill == PlayerSkillId.None || (!IsOpen && !_owner.CanPrepare && !_owner.CanUse))
        {
            _tooltip.gameObject.SetActive(false);
            return;
        }
        _tooltip.gameObject.SetActive(true);
        _tooltipTitle.text = SkillName(skill);
        string key = _owner.Loadout.Q == skill ? "Q" : _owner.Loadout.E == skill ? "E" : "";
        string hint = key.Length > 0 ? key + " · " : "";
        _tooltipDescription.text = hint + (skill switch
        {
            PlayerSkillId.Bomb => "발밑에 시한폭탄을 설치합니다.\n잠시 후 주변 적에게 범위 피해\n보유 수량: " + _owner.BombCount,
            PlayerSkillId.TemporaryTurret => "보조 포탑을 설치합니다.\n최대 3개 · 설치 간격 3초\n근처에서 F키로 회수",
            PlayerSkillId.EnergyBarrier => "발밑에 펄스 설치 → 같은 키로 연결\n연결 거리 1~18 · 지형 통과 불가\n몬스터 이동 차단 · 아군 탄환 통과\n보유 3개 · 18초마다 1개 충전\n우클릭으로 미완성 펄스 취소",
            PlayerSkillId.LureBeacon => "발밑에 주변 적을 유인하는 비콘 설치\n유인 반경 12 · 유지 8초\n체력 80 · 재사용 14초",
            _ => ""
        });
        var canvasRect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, GameInput.PointerPosition, null, out var point)) return;
        var bounds = canvasRect.rect;
        var size = _tooltip.sizeDelta;
        _tooltip.anchoredPosition = new Vector2(
            Mathf.Clamp(point.x + 18, bounds.xMin + 8, Mathf.Max(bounds.xMin + 8, bounds.xMax - size.x - 8)),
            Mathf.Clamp(point.y - 18, Mathf.Min(bounds.yMax - 8, bounds.yMin + size.y + 8), bounds.yMax - 8));
    }

    private void HideTooltip()
    {
        _hoveredSkill = null;
        if (_tooltip != null) _tooltip.gameObject.SetActive(false);
    }

    private void OnDisable() => HideTooltip();

    private Sprite Icon(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => _bombIcon,
        PlayerSkillId.TemporaryTurret => _turretIcon,
        PlayerSkillId.EnergyBarrier => _barrierIcon,
        PlayerSkillId.LureBeacon => _beaconIcon,
        _ => null
    };

    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Box(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text Label(string text, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color)
    {
        var rect = Rect("Label", parent, size);
        rect.anchoredPosition = position;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private Button TextButton(string title, Transform parent, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(title, parent, new Vector2(290, 44));
        rect.anchoredPosition = position;
        Box(rect, new Color(.08f, .24f, .28f)).raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        Label(title, rect, new Vector2(280, 40), Vector2.zero, 20, Color.white);
        return button;
    }

    private void OnDestroy()
    {
        if (_owner != null) _owner.Loadout.Changed -= Refresh;
        if (_cooldownMaskSprite != null)
        {
            if (Application.isPlaying) Destroy(_cooldownMaskSprite);
            else DestroyImmediate(_cooldownMaskSprite);
        }
    }
}

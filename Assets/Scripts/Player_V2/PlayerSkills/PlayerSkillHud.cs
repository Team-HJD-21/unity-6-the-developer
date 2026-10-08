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
    private static readonly PlayerSkillId[] Skills = { PlayerSkillId.Bomb, PlayerSkillId.TemporaryTurret };
    private PlayerSkillController _owner;
    private Sprite _bombIcon, _turretIcon;
    private TMP_FontAsset _font;
    private GameObject _modal;
    private TMP_Text _instruction;
    private readonly Slot[] _slots = new Slot[2];
    private readonly Button[] _cards = new Button[2];
    private readonly TMP_Text[] _cardLabels = new TMP_Text[2];
    private Button _reset;
    private Sprite _cooldownMaskSprite;
    private TMP_Text _recoveryPrompt;
    private Camera _worldCamera;
    public bool IsOpen => _modal != null && _modal.activeSelf && gameObject.activeInHierarchy;

    private sealed class Slot
    {
        public Button Button;
        public Image Border, Icon, Cooldown;
        public GameObject Empty;
        public TMP_Text Status;
    }

    public void Bind(PlayerSkillController owner, Sprite bombIcon, Sprite turretIcon, TMP_FontAsset font)
    {
        _owner = owner;
        _bombIcon = bombIcon;
        _turretIcon = turretIcon;
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
        _recoveryPrompt = Label("F  포탑 회수", transform, new Vector2(190, 32), Vector2.zero, 20, Cyan);
        _recoveryPrompt.gameObject.SetActive(false);
        CreateModal();
        _owner.Loadout.Changed += Refresh;
        Refresh();
    }

    private Slot CreateSlot(int index)
    {
        var root = Rect(index == 0 ? "Q Skill Slot" : "E Skill Slot", transform, new Vector2(88, 88));
        root.anchorMin = root.anchorMax = new Vector2(0, 1);
        root.pivot = new Vector2(0, 1);
        root.anchoredPosition = new Vector2(40, -180 - index * 104);
        var slot = new Slot { Border = Box(root, new Color(.22f, .39f, .43f)) };
        slot.Button = root.gameObject.AddComponent<Button>();
        slot.Button.onClick.AddListener(Open);
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
        var border = Rect("Panel Border", backdrop, new Vector2(760, 440));
        Box(border, Cyan);
        var panel = Rect("Skill Selection Panel", border, new Vector2(754, 434));
        Box(panel, Dark).raycastTarget = true;
        panel.gameObject.AddComponent<Button>(); // 패널의 빈 공간 클릭이 배경 닫기로 전달되지 않게 합니다.
        Label("스킬 선택", panel, new Vector2(630, 44), new Vector2(0, 170), 30, Color.white);
        _instruction = Label("", panel, new Vector2(670, 36), new Vector2(0, 124), 20, Cyan);
        for (int i = 0; i < Skills.Length; i++)
        {
            PlayerSkillId skill = Skills[i];
            var card = Rect(skill.ToString(), panel, new Vector2(320, 230));
            card.anchoredPosition = new Vector2(i == 0 ? -175 : 175, -15);
            Box(card, new Color(.075f, .19f, .23f)).raycastTarget = true;
            _cards[i] = card.gameObject.AddComponent<Button>();
            _cards[i].onClick.AddListener(() => Select(skill));
            var icon = Box(Rect("Icon", card, new Vector2(95, 95)), Color.white);
            icon.rectTransform.anchoredPosition = new Vector2(0, 48);
            icon.sprite = Icon(skill);
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;
            Label(skill == PlayerSkillId.Bomb ? "폭탄" : "보조 포탑", card,
                new Vector2(280, 34), new Vector2(0, -24), 25, Color.white);
            Label(skill == PlayerSkillId.Bomb ? "주변 적에게 범위 피해" : "3개 보유 · 설치 간격 3초\n근처에서 F키로 회수", card,
                new Vector2(280, 36), new Vector2(0, -60), 16, new Color(.65f, .8f, .83f));
            _cardLabels[i] = Label("선택", card, new Vector2(280, 28), new Vector2(0, -94), 19, Cyan);
        }
        _reset = TextButton("선택 초기화", panel, new Vector2(-170, -177), _owner.ClearSelection);
        TextButton("닫기", panel, new Vector2(170, -177), Close);
        _modal.SetActive(false);
    }

    public void Open()
    {
        if (!_owner.CanPrepare) return;
        _modal.SetActive(true);
        Refresh();
        RefreshRecoveryPrompt();
    }

    private void RefreshRecoveryPrompt()
    {
        Transform turret = _owner.RecoverableTurret;
        if (_worldCamera == null) _worldCamera = Camera.main;
        bool visible = turret != null && _worldCamera != null && !IsOpen;
        _recoveryPrompt.gameObject.SetActive(visible);
        if (!visible) return;
        Vector3 screen = _worldCamera.WorldToScreenPoint(turret.position + Vector3.up * 1.1f);
        if (screen.z <= 0) { _recoveryPrompt.gameObject.SetActive(false); return; }
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
            screen, null, out Vector2 point);
        _recoveryPrompt.rectTransform.anchoredPosition = point;
    }

    public void Close() { if (_modal != null) _modal.SetActive(false); }

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
    }

    private void Refresh()
    {
        if (_owner == null) return;
        for (int i = 0; i < _slots.Length; i++)
        {
            var skill = i == 0 ? _owner.Loadout.Q : _owner.Loadout.E;
            var slot = _slots[i];
            if (slot == null) continue;
            bool assigned = skill != PlayerSkillId.None;
            slot.Empty.SetActive(!assigned);
            slot.Icon.sprite = Icon(skill);
            slot.Icon.enabled = assigned && slot.Icon.sprite != null;
            float remaining = _owner.RemainingCooldown(skill);
            int count = skill == PlayerSkillId.Bomb ? _owner.BombCount : _owner.TurretCount;
            bool ready = assigned && remaining <= 0 && count > 0;
            slot.Border.color = assigned ? (ready ? Cyan : new Color(.2f, .5f, .55f)) : new Color(.22f, .39f, .43f);
            slot.Icon.color = count > 0 ? Color.white : new Color(.4f, .5f, .55f);
            slot.Cooldown.enabled = assigned && remaining > 0;
            slot.Cooldown.fillAmount = _owner.CooldownProgress(skill);
            slot.Status.text = assigned ? count.ToString() : "";
            // 대화/일시정지 UI를 가로채지 않고 전투 중에는 상태만 표시합니다.
            slot.Border.raycastTarget = _owner.CanPrepare;
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

    private Sprite Icon(PlayerSkillId skill) => skill == PlayerSkillId.Bomb ? _bombIcon :
        skill == PlayerSkillId.TemporaryTurret ? _turretIcon : null;

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

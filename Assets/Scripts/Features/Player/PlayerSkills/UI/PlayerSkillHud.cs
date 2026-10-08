// Player 스킬의 빈 슬롯, 준비 중 선택 패널, 선택된 아이콘과 쿨타임을 표시하는 uGUI View입니다.
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class PlayerSkillHud : MonoBehaviour
{
    private PlayerSkillController _owner;
    private Sprite _bombIcon, _turretIcon, _barrierIcon, _beaconIcon;
    private TMP_FontAsset _font;
    private Sprite _cooldownMaskSprite;
    private TMP_Text _placementHint;
    private PlayerSkillUiFactory _ui;
    private PlayerSkillTooltipView _tooltip;
    private PlayerTurretRecoveryPromptView _recovery;
    private PlayerSkillSelectionPanelView _panel;
    private readonly PlayerSkillSlotView[] _slots = new PlayerSkillSlotView[2];
    public bool IsOpen => _panel != null && _panel.IsOpen && gameObject.activeInHierarchy;
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
        _ui = new PlayerSkillUiFactory(_font);
        _tooltip = new PlayerSkillTooltipView((RectTransform)transform, _owner, _ui, () => IsOpen);
        for (int i = 0; i < _slots.Length; i++) _slots[i] = CreateSlot(i, null);
        _recovery = new PlayerTurretRecoveryPromptView((RectTransform)transform, _owner, _ui, () => IsOpen, recoveryKeyIcon);
        _placementHint = _ui.Label("", transform, new Vector2(1050, 40), new Vector2(0, -380), 24, PlayerSkillUiFactory.Cyan);
        _panel = new PlayerSkillSelectionPanelView(transform, _owner, _ui, _tooltip, Icon, CreateSlot, Close, Select);
        _owner.Loadout.Changed += Refresh;
        Refresh();
    }
    public void Open()
    {
        if (!_owner.CanPrepare) return;
        _tooltip.Hide();
        _panel.SetOpen(true);
        Refresh();
        _recovery.Refresh();
    }
    public void Close()
    {
        _tooltip?.Hide();
        _panel?.SetOpen(false);
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
        _recovery.Refresh();
        _tooltip.Refresh();
        if (_placementHint != null)
        {
            _placementHint.text = _owner.BarrierPlacementHint;
            _placementHint.gameObject.SetActive(!IsOpen && _placementHint.text.Length > 0);
        }
    }
    private PlayerSkillSlotView CreateSlot(int index, Transform parent) =>
        PlayerSkillSlotView.Create(index, parent, transform, _owner, _ui, Icon, _cooldownMaskSprite, _tooltip, Open);
    private void Refresh()
    {
        if (_owner == null) return;
        for (int i = 0; i < _slots.Length; i++) _slots[i].Refresh(i == 0 ? _owner.Loadout.Q : _owner.Loadout.E);
        _panel.Refresh();
    }
    private void OnDisable() => _tooltip?.Hide();
    private Sprite Icon(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => _bombIcon,
        PlayerSkillId.TemporaryTurret => _turretIcon,
        PlayerSkillId.EnergyBarrier => _barrierIcon,
        PlayerSkillId.LureBeacon => _beaconIcon,
        _ => null
    };

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

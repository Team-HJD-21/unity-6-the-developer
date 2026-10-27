using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>씬이 소유한 스킬 UI에 연결된 플레이어의 상태를 표시합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class PlayerSkillHud : MonoBehaviour
{
    [Header("Authored Views")]
    [SerializeField] private PlayerSkillSlotView _qSlot;
    [SerializeField] private PlayerSkillSlotView _eSlot;
    [SerializeField] private PlayerSkillSelectionPanelView _panel;
    [SerializeField] private PlayerSkillTooltipView _tooltip;
    [SerializeField] private PlayerTurretRecoveryPromptView _recovery;
    [SerializeField] private TMP_Text _placementHint;
    [SerializeField] private TMP_Text _placementFailure;
    [Header("Skill Icons")]
    [SerializeField] private Sprite _bombIcon;
    [SerializeField] private Sprite _turretIcon;
    [SerializeField] private Sprite _barrierIcon;
    [SerializeField] private Sprite _beaconIcon;
    private Canvas _canvas;
    private GraphicRaycaster _raycaster;
    public PlayerSkillController Owner { get; private set; }
    public bool IsOpen => Owner != null && _panel != null && _panel.IsOpen;
    public bool IsConfigured => _qSlot != null && _qSlot.IsConfigured
        && _eSlot != null && _eSlot.IsConfigured && _panel != null && _panel.IsConfigured;
    public bool IsFullyConfigured => IsConfigured && HasTooltip && _recovery != null
        && _placementHint != null && _placementFailure != null
        && _bombIcon != null && _turretIcon != null && _barrierIcon != null && _beaconIcon != null;
    private bool HasTooltip => _tooltip != null && _tooltip.IsConfigured;

    private void Awake()
    {
        SetVisible(false);
        if (!IsConfigured) Debug.LogError("Player Skill HUD needs configured Q/E slots and a selection panel.", this);
        else WarnAboutOptionalReferences();
    }

    private void WarnAboutOptionalReferences()
    {
        if (IsFullyConfigured) return;
        var missing = new List<string>();
        if (!HasTooltip) missing.Add(nameof(_tooltip));
        if (_recovery == null) missing.Add(nameof(_recovery));
        if (_placementHint == null) missing.Add(nameof(_placementHint));
        if (_placementFailure == null) missing.Add(nameof(_placementFailure));
        if (_bombIcon == null) missing.Add(nameof(_bombIcon));
        if (_turretIcon == null) missing.Add(nameof(_turretIcon));
        if (_barrierIcon == null) missing.Add(nameof(_barrierIcon));
        if (_beaconIcon == null) missing.Add(nameof(_beaconIcon));
        Debug.LogWarning($"Player Skill HUD: unavailable optional references: {string.Join(", ", missing)}. Skill selection remains available.", this);
    }

    // PlayerSkillController.BindHud가 양방향 참조를 연결합니다.
    internal bool Bind(PlayerSkillController owner)
    {
        if (!IsConfigured || !isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled
            || owner.gameObject.scene != gameObject.scene) return false;
        if (Owner == owner) return true;
        Unbind();
        Owner = owner;
        _qSlot.Bind(this);
        _eSlot.Bind(this);
        _panel.Bind(this);
        if (_tooltip != null) _tooltip.Bind(HasTooltip ? this : null);
        if (_recovery != null) _recovery.Bind(this);
        Owner.Loadout.Changed += OnLoadoutChanged;
        SetVisible(true);
        OnLoadoutChanged();
        RefreshPlacementFeedback();
        return true;
    }

    public void Unbind()
    {
        var previousOwner = Owner;
        if (previousOwner != null) previousOwner.Loadout.Changed -= OnLoadoutChanged;
        Owner = null;
        if (_qSlot != null) _qSlot.Unbind();
        if (_eSlot != null) _eSlot.Unbind();
        if (_panel != null) _panel.Unbind();
        if (_tooltip != null) _tooltip.Bind(null);
        if (_recovery != null) _recovery.Bind(null);
        if (_placementHint != null) _placementHint.gameObject.SetActive(false);
        if (_placementFailure != null) _placementFailure.gameObject.SetActive(false);
        SetVisible(false);
        if (previousOwner != null) previousOwner.DetachHud(this);
    }

    public void Open()
    {
        if (Owner == null || !Owner.CanPrepare) return;
        if (_tooltip != null) _tooltip.Hide();
        _panel.SetOpen(true);
        OnLoadoutChanged();
        RefreshPlacementFeedback();
        if (_recovery != null) _recovery.Refresh();
    }

    public void Close()
    {
        if (_tooltip != null) _tooltip.Hide();
        if (_panel != null) _panel.SetOpen(false);
    }

    public void Select(PlayerSkillId skill)
    {
        if (Owner == null || !Owner.TrySelect(skill)) return;
        if (Owner.Loadout.E != PlayerSkillId.None) Close();
    }

    public Sprite Icon(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => _bombIcon,
        PlayerSkillId.PlayerTurret => _turretIcon,
        PlayerSkillId.EnergyBarrier => _barrierIcon,
        PlayerSkillId.LureBeacon => _beaconIcon,
        _ => null
    };
    public void ShowTooltip(Object source, PlayerSkillId skill)
    {
        if (HasTooltip) _tooltip.Show(source, skill);
    }

    public void HideTooltip(Object source)
    {
        if (_tooltip != null) _tooltip.Hide(source);
    }

    private void LateUpdate()
    {
        if (Owner == null || !Owner.isActiveAndEnabled)
        {
            if (Owner != null) Unbind();
            return;
        }
        if (IsOpen && (!Owner.CanPrepare || GameInput.WasPressedThisFrame(GameKey.Escape))) Close();
        RefreshSlots();
        if (IsOpen) _panel.RefreshCooldowns();
        if (_tooltip != null)
        {
            if (HasTooltip) _tooltip.Refresh();
            else _tooltip.Hide();
        }
        if (_recovery != null) _recovery.Refresh();
        RefreshPlacementFeedback();
    }

    private void OnLoadoutChanged()
    {
        if (Owner == null) return;
        RefreshSlots();
        if (!IsOpen) return;
        _panel.RefreshSelection();
        _panel.RefreshCooldowns();
    }

    private void RefreshSlots()
    {
        if (Owner == null) return;
        _qSlot.Refresh();
        _eSlot.Refresh();
    }

    private void RefreshPlacementFeedback()
    {
        if (Owner == null) return;
        string failure = PlayerSkillDescriptions.TurretPlacementFailure(Owner.TurretPlacementFeedback);
        bool showsFailure = _placementFailure != null && failure.Length > 0;
        if (_placementFailure != null)
        {
            _placementFailure.text = failure;
            _placementFailure.gameObject.SetActive(!IsOpen && showsFailure);
        }
        if (_placementHint != null)
        {
            _placementHint.text = Owner.PlacementHint;
            _placementHint.gameObject.SetActive(!IsOpen && !showsFailure && _placementHint.text.Length > 0);
        }
    }

    private void SetVisible(bool visible)
    {
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        if (_raycaster == null) _raycaster = GetComponent<GraphicRaycaster>();
        _canvas.enabled = visible;
        _raycaster.enabled = visible;
    }
    private void OnDisable() => Unbind();
}

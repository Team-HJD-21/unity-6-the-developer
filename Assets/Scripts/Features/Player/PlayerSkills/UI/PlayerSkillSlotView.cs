using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>미리 배치된 Q/E 슬롯의 아이콘·수량·쿨타임을 갱신합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerSkillSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Range(0, 1)] private int _slotIndex;
    [SerializeField] private bool _opensSelection = true;
    [SerializeField] private Button _button;
    [SerializeField] private Image _border;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _cooldown;
    [SerializeField] private GameObject _empty;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private CombatHudArt _combatArt;
    [SerializeField] private CombatSkillSlotFeedback _inputFeedback;
    [Header("State Colors")]
    [SerializeField] private Color _readyColor = new(.35f, .95f, 1);
    [SerializeField] private Color _unreadyColor = new(.2f, .5f, .55f);
    [SerializeField] private Color _emptyColor = new(.22f, .39f, .43f);
    [SerializeField] private Color _depletedIconColor = new(.4f, .5f, .55f);
    [SerializeField] private Color _cooldownColor = new(.28f, .3f, .32f, .85f);
    [SerializeField, Range(0, 1)] private float _partialChargeAlpha = .35f;
    private PlayerSkillHud _hud;
    private PlayerSkillController _inputOwner;
    private PlayerSkillId _displayedSkill;
    private int _displayedCount = -1;
    public bool IsConfigured => _button != null && _border != null && _icon != null
        && _cooldown != null && _cooldown.sprite != null && _empty != null && _status != null;
    private PlayerSkillId Skill => _hud == null || _hud.Owner == null ? PlayerSkillId.None
        : _slotIndex == 0 ? _hud.Owner.Loadout.Q : _hud.Owner.Loadout.E;

    public void Bind(PlayerSkillHud hud)
    {
        Unbind();
        _hud = hud;
        _displayedCount = -1;
        _inputOwner = _hud.Owner;
        if (_inputOwner != null) _inputOwner.SlotUseAttempted += OnUseAttempted;
        if (_opensSelection) _button.onClick.AddListener(OpenSelection);
    }
    public void Unbind()
    {
        if (_inputOwner != null) _inputOwner.SlotUseAttempted -= OnUseAttempted;
        _inputOwner = null;
        if (_hud != null) _hud.HideTooltip(this);
        if (_button != null) _button.onClick.RemoveListener(OpenSelection);
        _hud = null;
    }
    private void OpenSelection() => _hud?.Open();
    private void OnUseAttempted(int slot, PlayerSkillId skill, bool accepted)
    { if (slot == _slotIndex && _inputFeedback != null) _inputFeedback.Press(accepted); }

    public void Refresh()
    {
        if (_hud == null || _hud.Owner == null) return;
        var owner = _hud.Owner;
        var skill = Skill;
        bool assigned = skill != PlayerSkillId.None;
        int count = owner.SkillCount(skill);
        _empty.SetActive(!assigned);
        if (_combatArt != null) _combatArt.Show(skill);
        else
        {
            _icon.sprite = _hud.Icon(skill);
            _icon.enabled = assigned && _icon.sprite != null;
        }
        _border.color = !assigned ? _emptyColor : owner.IsSkillReady(skill) ? _readyColor : _unreadyColor;
        _icon.color = count > 0 ? Color.white : _depletedIconColor;
        _cooldown.enabled = assigned && owner.RemainingCooldown(skill) > 0;
        var color = _cooldownColor;
        if (skill == PlayerSkillId.EnergyBarrier && count > 0) color.a = _partialChargeAlpha;
        _cooldown.color = color;
        _cooldown.fillAmount = owner.CooldownProgress(skill);
        RefreshCount(skill, count);
        // 전투 중에는 툴팁을 유지하되 대화/일시정지 입력은 가로채지 않습니다.
        _border.raycastTarget = owner.CanPrepare || owner.CanUse;
    }

    private void RefreshCount(PlayerSkillId skill, int count)
    {
        if (_displayedSkill == skill && _displayedCount == count) return;
        _displayedSkill = skill;
        _displayedCount = count;
        _status.text = skill != PlayerSkillId.None ? count.ToString() : "";
    }
    public void OnPointerEnter(PointerEventData eventData) => _hud?.ShowTooltip(this, Skill);
    public void OnPointerExit(PointerEventData eventData) => _hud?.HideTooltip(this);
    private void OnDisable() { if (_hud != null) _hud.HideTooltip(this); }
}

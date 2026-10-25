using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>스킬 카드 하나의 선택 가능 상태와 포인터 안내를 표시합니다.</summary>
public sealed class PlayerSkillCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private PlayerSkillId _skill;
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _state;
    private PlayerSkillHud _hud;
    public bool IsConfigured => _skill != PlayerSkillId.None && _button != null && _icon != null && _state != null;
    public void Bind(PlayerSkillHud hud)
    {
        Unbind();
        _hud = hud;
        _button.onClick.AddListener(Select);
        _icon.sprite = hud.Icon(_skill);
        _icon.enabled = _icon.sprite != null;
    }
    public void Unbind()
    {
        if (_hud != null) _hud.HideTooltip(this);
        if (_button != null) _button.onClick.RemoveListener(Select);
        _hud = null;
    }
    private void Select() => _hud?.Select(_skill);
    public void Refresh()
    {
        if (_hud == null || _hud.Owner == null) return;
        var owner = _hud.Owner;
        bool chosen = owner.Loadout.Contains(_skill);
        bool available = owner.IsAvailable(_skill);
        _button.interactable = owner.CanPrepare && owner.Loadout.E == PlayerSkillId.None && !chosen && available;
        _state.text = chosen ? (owner.Loadout.Q == _skill ? "Q 등록됨" : "E 등록됨") : available ? "선택" : "준비되지 않음";
    }
    public void OnPointerEnter(PointerEventData eventData) => _hud?.ShowTooltip(this, _skill);
    public void OnPointerExit(PointerEventData eventData) => _hud?.HideTooltip(this);
    private void OnDisable() { if (_hud != null) _hud.HideTooltip(this); }
}

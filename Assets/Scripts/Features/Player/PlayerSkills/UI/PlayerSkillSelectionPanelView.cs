using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>프리팹에 배치된 선택 카드와 Q/E 미리보기를 연결합니다.</summary>
public sealed class PlayerSkillSelectionPanelView : MonoBehaviour
{
    [SerializeField] private Button _backdrop;
    [SerializeField] private Button _close;
    [SerializeField] private Button _reset;
    [SerializeField] private TMP_Text _instruction;
    [SerializeField] private PlayerSkillCardView[] _cards;
    [SerializeField] private PlayerSkillSlotView _qPreview;
    [SerializeField] private PlayerSkillSlotView _ePreview;
    [SerializeField] private TMP_Text _qLabel;
    [SerializeField] private TMP_Text _eLabel;
    private PlayerSkillHud _hud;
    public bool IsOpen => gameObject.activeSelf;
    public bool IsConfigured
    {
        get
        {
            if (_backdrop == null || _close == null || _reset == null || _instruction == null
                || _qPreview == null || !_qPreview.IsConfigured || _ePreview == null || !_ePreview.IsConfigured
                || _qLabel == null || _eLabel == null || _cards == null || _cards.Length != 4) return false;
            foreach (var card in _cards) if (card == null || !card.IsConfigured) return false;
            return true;
        }
    }
    public void Bind(PlayerSkillHud hud)
    {
        Unbind();
        _hud = hud;
        _backdrop.onClick.AddListener(Close);
        _close.onClick.AddListener(Close);
        _reset.onClick.AddListener(ResetSelection);
        _qPreview.Bind(hud);
        _ePreview.Bind(hud);
        foreach (var card in _cards) card.Bind(hud);
    }
    public void Unbind()
    {
        if (_backdrop != null) _backdrop.onClick.RemoveListener(Close);
        if (_close != null) _close.onClick.RemoveListener(Close);
        if (_reset != null) _reset.onClick.RemoveListener(ResetSelection);
        _qPreview?.Unbind();
        _ePreview?.Unbind();
        if (_cards != null) foreach (var card in _cards) card?.Unbind();
        _hud = null;
        SetOpen(false);
    }
    public void SetOpen(bool open) => gameObject.SetActive(open);
    private void Close() => _hud?.Close();
    private void ResetSelection() { if (_hud != null && _hud.Owner != null) _hud.Owner.ClearSelection(); }
    /// <summary>선택 변경과 창 열기 시에 카드·설명·선택 이름을 갱신합니다.</summary>
    public void RefreshSelection()
    {
        if (!IsOpen || _hud == null || _hud.Owner == null) return;
        var owner = _hud.Owner;
        _qLabel.text = SelectionName(owner.Loadout.Q);
        _eLabel.text = SelectionName(owner.Loadout.E);
        bool full = owner.Loadout.E != PlayerSkillId.None;
        _instruction.text = full ? "Q / E 등록 완료 · 변경하려면 선택을 초기화하세요" :
            owner.Loadout.Q == PlayerSkillId.None ? "먼저 선택한 스킬 → Q   /   다음 스킬 → E" : "Q 등록 완료 · 다음 스킬은 E에 등록됩니다";
        foreach (var card in _cards) card.Refresh();
        _reset.interactable = owner.CanPrepare && owner.Loadout.Q != PlayerSkillId.None;
    }

    /// <summary>열린 창에서만 미리보기 슬롯의 실시간 상태를 갱신합니다.</summary>
    public void RefreshCooldowns()
    {
        if (!IsOpen || _hud == null || _hud.Owner == null) return;
        _qPreview.Refresh();
        _ePreview.Refresh();
    }
    private static string SelectionName(PlayerSkillId skill) =>
        skill == PlayerSkillId.None ? "미선택" : PlayerSkillDescriptions.Name(skill);
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>제작된 스킬 회로 UI의 열기/복귀와 스크롤만 담당합니다. 스킬 상태와 해금 포인트는 변경하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerSkillTreePanel : MonoBehaviour
{
    [System.Serializable]
    private sealed class SkillCard
    {
        public PlayerSkillId Skill;
        public TMP_Text Title;
        public TMP_Text Summary;
        public Image Icon;
        public bool IsConfigured => Title != null && Summary != null && Icon != null &&
            (Skill == PlayerSkillId.None || Icon.sprite != null);
    }
    [SerializeField] private PlayerStatusPanel _status;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private GameObject[] _statusSections;
    [SerializeField] private SkillCard[] _cards;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;
    public bool IsConfigured => _status != null && _panelRoot != null && _panelRoot != gameObject &&
        _panelRoot.transform.IsChildOf(transform) && _openButton != null && _backButton != null &&
        _scroll != null && _scroll.content != null && _scroll.viewport != null &&
        _statusSections != null && _statusSections.Length > 0 &&
        System.Array.TrueForAll(_statusSections, section => section != null && section != gameObject && section != _panelRoot) &&
        _cards != null && _cards.Length == 8 && System.Array.TrueForAll(_cards, card => card != null && card.IsConfigured);

    private void OnEnable()
    {
        Close();
        if (_openButton != null) _openButton.onClick.AddListener(Open);
        if (_backButton != null) _backButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        if (_openButton != null) _openButton.onClick.RemoveListener(Open);
        if (_backButton != null) _backButton.onClick.RemoveListener(Close);
        Close();
    }

    public void Open()
    {
        GameInput.TryConsumePrimaryPointerPress();
        if (!isActiveAndEnabled || !IsConfigured || !_status.isActiveAndEnabled || !_status.IsOpen) return;
        foreach (var card in _cards)
        {
            bool assigned = card.Skill != PlayerSkillId.None;
            card.Title.text = assigned ? PlayerSkillDescriptions.Name(card.Skill) : string.Empty;
            card.Summary.text = assigned ? PlayerSkillDescriptions.Summary(card.Skill) : string.Empty;
            card.Icon.enabled = assigned && card.Icon.sprite != null;
        }
        foreach (var section in _statusSections) section.SetActive(false);
        _panelRoot.SetActive(true);
        _scroll.StopMovement();
        _scroll.content.anchoredPosition = Vector2.zero;
        _scroll.verticalNormalizedPosition = 1;
    }

    public bool TryClose()
    {
        if (!IsOpen) return false;
        GameInput.TryConsumePrimaryPointerPress();
        _panelRoot.SetActive(false);
        if (_statusSections != null)
            foreach (var section in _statusSections) if (section != null) section.SetActive(true);
        return true;
    }

    public void Close() => TryClose();
}

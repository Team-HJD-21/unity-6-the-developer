using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>제작된 트리에서 원본 해금 요청과 하단 후보 네 칸 배치를 연결합니다.</summary>
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
        public Button Button;
        public TMP_Text State;
        public bool IsConfigured => Title != null && Summary != null && Icon != null &&
            Button != null && State != null;
    }
    [SerializeField] private PlayerStatusPanel _status;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private GameObject[] _statusSections;
    [SerializeField] private SkillCard[] _cards;
    [SerializeField] private TMP_Text _tokens, _selectionText;
    [SerializeField] private Button _unlock;
    [SerializeField] private Button _upgrade;
    [SerializeField] private TMP_Text _upgradeStats;
    [SerializeField] private Button[] _candidateButtons;
    [SerializeField] private Image[] _candidateIcons;
    [SerializeField] private TMP_Text[] _candidateNames, _candidateStates;
    [SerializeField] private RectTransform _window, _windowBackground;
    private Vector2 _originalWindowSize, _originalBackgroundSize;
    private bool _windowExpanded;
    private PlayerSkillController _owner;
    private PlayerSkillId _selected = PlayerSkillId.Bomb;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;
    public bool IsConfigured => _status != null && _panelRoot != null && _panelRoot != gameObject &&
        _panelRoot.transform.IsChildOf(transform) && _openButton != null && _backButton != null &&
        _scroll != null && _scroll.content != null && _scroll.viewport != null &&
        _statusSections != null && _statusSections.Length > 0 &&
        System.Array.TrueForAll(_statusSections, section => section != null && section != gameObject && section != _panelRoot) &&
        _cards != null && _cards.Length >= 6 && System.Array.TrueForAll(_cards, card => card != null && card.IsConfigured) &&
        _tokens != null && _selectionText != null && _unlock != null && _upgrade != null && _upgradeStats != null && _candidateButtons != null && _candidateButtons.Length == 4 &&
        System.Array.TrueForAll(_candidateButtons, button => button != null) &&
        _candidateIcons != null && _candidateIcons.Length == 4 && System.Array.TrueForAll(_candidateIcons, icon => icon != null) &&
        _candidateNames != null && _candidateNames.Length == 4 && System.Array.TrueForAll(_candidateNames, text => text != null) &&
        _candidateStates != null && _candidateStates.Length == 4 && System.Array.TrueForAll(_candidateStates, text => text != null) &&
        _window != null && _windowBackground != null;

    private void OnEnable()
    {
        Close();
        if (_openButton != null) _openButton.onClick.AddListener(Open);
        if (_backButton != null) _backButton.onClick.AddListener(Close);
        if (_unlock != null) _unlock.onClick.AddListener(Unlock);
        if (_upgrade != null) _upgrade.onClick.AddListener(Upgrade);
        if (_cards != null) foreach (var card in _cards)
        { if (card?.Button == null) continue; var id = card.Skill; card.Button.onClick.AddListener(() => Select(id)); }
        if (_candidateButtons != null) for (int i=0;i<_candidateButtons.Length;i++)
        { int slot=i; if (_candidateButtons[i] != null) _candidateButtons[i].onClick.AddListener(() => Assign(slot)); }
    }

    private void OnDisable()
    {
        if (_openButton != null) _openButton.onClick.RemoveListener(Open);
        if (_backButton != null) _backButton.onClick.RemoveListener(Close);
        if (_unlock != null) _unlock.onClick.RemoveListener(Unlock);
        if (_upgrade != null) _upgrade.onClick.RemoveListener(Upgrade);
        if (_cards != null) foreach(var card in _cards) card?.Button?.onClick.RemoveAllListeners();
        if (_candidateButtons != null) foreach(var button in _candidateButtons) if(button != null) button.onClick.RemoveAllListeners();
        Close();
    }

    public void Open()
    {
        GameInput.TryConsumePrimaryPointerPress();
        if (!isActiveAndEnabled || !IsConfigured || !_status.isActiveAndEnabled || !_status.IsOpen) return;
        _owner = _status.Owner != null ? _status.Owner.GetComponent<PlayerSkillController>() : null;
        _selected = PlayerSkillId.Bomb;
        foreach (var card in _cards)
        {
            bool assigned = card.Skill != PlayerSkillId.None;
            card.Title.text = assigned ? PlayerSkillDescriptions.Name(card.Skill) : string.Empty;
            card.Summary.text = assigned ? PlayerSkillDescriptions.Summary(card.Skill) : string.Empty;
            card.Icon.enabled = assigned && card.Icon.sprite != null;
        }
        foreach (var section in _statusSections) section.SetActive(false);
        if (!_windowExpanded)
        {
            _originalWindowSize = _window.sizeDelta; _originalBackgroundSize = _windowBackground.sizeDelta;
            _window.sizeDelta = new Vector2(_window.sizeDelta.x, 960);
            _windowBackground.sizeDelta = new Vector2(_windowBackground.sizeDelta.x, 956);
            _windowExpanded = true;
        }
        _panelRoot.SetActive(true);
        _scroll.StopMovement();
        _scroll.content.anchoredPosition = Vector2.zero;
        _scroll.verticalNormalizedPosition = 1;
        Refresh();
    }

    public bool TryClose()
    {
        if (!IsOpen) return false;
        GameInput.TryConsumePrimaryPointerPress();
        _panelRoot.SetActive(false);
        if (_windowExpanded)
        {
            _window.sizeDelta = _originalWindowSize; _windowBackground.sizeDelta = _originalBackgroundSize;
            _windowExpanded = false;
        }
        if (_statusSections != null)
            foreach (var section in _statusSections) if (section != null) section.SetActive(true);
        return true;
    }

    public void Close() => TryClose();

    public void Select(PlayerSkillId skill) { _selected = skill; Refresh(); }
    public bool Assign(int slot)
    {
        GameInput.TryConsumePrimaryPointerPress();
        bool accepted = IsOpen && _owner != null && _owner.Research != null && _owner.Research.TrySetCandidate(slot, _selected);
        Refresh(); return accepted;
    }
    public void Unlock()
    { GameInput.TryConsumePrimaryPointerPress(); if (IsOpen && _owner != null) _owner.Research?.TryUnlock(_selected); Refresh(); }
    public void Upgrade()
    { GameInput.TryConsumePrimaryPointerPress(); if (IsOpen && _owner != null) _owner.Research?.TryUpgrade(_selected); Refresh(); }
    private void LateUpdate() { if (IsOpen) Refresh(); }
    private void Refresh()
    {
        if (!IsOpen || _owner == null || _owner.Research == null) return;
        var research = _owner.Research;
        _tokens.text = $"스킬 토큰  {research.Tokens}개   /   레벨업마다 1개";
        foreach(var card in _cards)
        {
            bool unlocked = research.IsUnlocked(card.Skill);
            bool configured = _owner.IsConfiguredSkill(card.Skill);
            card.Title.text = PlayerSkillDescriptions.Name(card.Skill);
            card.Summary.text = PlayerSkillDescriptions.Summary(card.Skill);
            card.Icon.enabled = card.Icon.sprite != null;
            bool selected = card.Skill == _selected;
            card.State.text = (!configured ? "준비 중" : unlocked ? $"{research.Level(card.Skill)}레벨 / {PlayerSkillResearch.MaximumLevel}" :
                research.IsUnlocked(PlayerSkillResearch.Prerequisite(card.Skill)) ? "해금 가능 · 토큰 1개" : "잠김 · 선행 스킬 필요") + (selected ? "  ·  선택됨" : "");
            card.Button.image.color = selected ? new Color(.25f,.24f,.15f) : unlocked ? new Color(.10f,.20f,.22f) : new Color(.07f,.10f,.12f);
            card.Title.color = unlocked || selected ? new Color(.95f,.94f,.87f) : new Color(.60f,.65f,.67f);
            card.Summary.color = unlocked || selected ? new Color(.82f,.86f,.85f) : new Color(.48f,.55f,.57f);
            card.State.color = selected ? new Color(.96f,.84f,.49f) : unlocked ? new Color(.47f,.81f,.78f) : new Color(.67f,.66f,.58f);
            if (card.Button.TryGetComponent<Outline>(out var border)) border.effectColor = selected ? new Color(.84f,.75f,.49f) : unlocked ? new Color(.25f,.46f,.48f) : new Color(.2f,.24f,.27f);
        }
        _selectionText.text = "선택: " + PlayerSkillDescriptions.Name(_selected) + (research.IsUnlocked(_selected) ? $"  ·  {research.Level(_selected)}레벨" :
            !_owner.IsConfiguredSkill(_selected) ? "  /  준비 중" :
            research.IsUnlocked(PlayerSkillResearch.Prerequisite(_selected)) ? "  /  토큰 1개로 해금" :
            "  /  선행: " + PlayerSkillDescriptions.Name(PlayerSkillResearch.Prerequisite(_selected)));
        _unlock.interactable = research.CanUnlock(_selected);
        _unlock.GetComponentInChildren<TMP_Text>().text = research.IsUnlocked(_selected) ? "해금 완료" : "해금 · 토큰 1개";
        int level = research.Level(_selected);
        float current = _owner.UpgradeStatValue(_selected);
        float next = level > 0 && level < PlayerSkillResearch.MaximumLevel ? current / research.Multiplier(_selected) * (1 + .15f * level) : current;
        string suffix = _selected == PlayerSkillId.DualWield ? "%" : "";
        _upgradeStats.text = level == 0 ? "해금 후 강화할 수 있습니다." : level >= PlayerSkillResearch.MaximumLevel ?
            $"{_owner.UpgradeStatName(_selected)}  {current:0.#}{suffix}  ·  최고 레벨" :
            $"{_owner.UpgradeStatName(_selected)}  {current:0.#}{suffix}  >  {next:0.#}{suffix}";
        _upgrade.interactable = research.CanUpgrade(_selected);
        _upgrade.GetComponentInChildren<TMP_Text>().text = level >= PlayerSkillResearch.MaximumLevel ? "최고 레벨" : "강화 · 토큰 1개";
        for (int i=0;i<4;i++)
        {
            var id=research.Candidate(i);
            bool assigned = id != PlayerSkillId.None, selected = assigned && id == _selected;
            bool canAssign = research.CanModify && research.IsUnlocked(_selected) && _owner.IsConfiguredSkill(_selected);
            _candidateNames[i].text = assigned ? PlayerSkillDescriptions.Name(id) : "빈 슬롯";
            _candidateNames[i].rectTransform.anchoredPosition = new Vector2(0, assigned ? -20 : 4);
            _candidateIcons[i].sprite = System.Array.Find(_cards, card => card.Skill == id)?.Icon.sprite;
            _candidateIcons[i].enabled = assigned && _candidateIcons[i].sprite != null;
            _candidateStates[i].text = selected ? "배치됨" : !canAssign ? "해금한 스킬을 선택" : assigned ? "클릭하여 교체" : "클릭하여 배치";
            _candidateButtons[i].interactable = canAssign && !selected;
            _candidateButtons[i].image.color = selected ? new Color(.25f,.24f,.15f) : assigned ? new Color(.10f,.20f,.22f) : new Color(.065f,.09f,.115f);
            if (_candidateButtons[i].TryGetComponent<Outline>(out var border)) border.effectColor = selected ? new Color(.92f,.79f,.43f) : canAssign ? new Color(.40f,.64f,.64f) : new Color(.24f,.29f,.31f);
        }
    }
}

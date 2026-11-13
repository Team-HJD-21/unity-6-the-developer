using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 씬 HUD의 로컬 소유자를 따라 Space 대시의 준비·쿨타임만 표시합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDashHud : MonoBehaviour
{
    [SerializeField] private PlayerSkillHud _skillHud;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Image _cooldownFill;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private CombatSkillSlotFeedback _inputFeedback;
    [SerializeField] private Color _readyColor = new(.35f, .95f, 1f);
    [SerializeField] private Color _blockedColor = new(.55f, .63f, .66f);
    private PlayerSkillController _boundPlayer;
    private PlayerDash _dash;
    public PlayerDash Owner => _dash;
    public bool IsConfigured => _skillHud != null && _group != null && _cooldownFill != null && _status != null;

    private void Awake()
    {
        if (!IsConfigured) Debug.LogError("[PlayerDashHud] Connect the authored Skill HUD, CanvasGroup, fill and status label.", this);
    }

    private void LateUpdate()
    {
        if (!IsConfigured) return;
        if (_boundPlayer != _skillHud.Owner)
        {
            if (_dash != null) _dash.DashInputAttempted -= OnDashInput;
            _boundPlayer = _skillHud.Owner;
            _dash = _boundPlayer != null ? _boundPlayer.GetComponent<PlayerDash>() : null;
            if (_dash != null) _dash.DashInputAttempted += OnDashInput;
        }
        bool visible = _dash != null && _dash.isActiveAndEnabled;
        _group.alpha = visible ? 1 : 0;
        if (!visible) return;
        _cooldownFill.fillAmount = _dash.CooldownProgress;
        _status.color = _dash.IsReady ? _readyColor : _blockedColor;
        // SPACE는 고정 키입니다. 쿨타임은 Filled 이미지로만 나타내며 초 카운트다운을 덧붙이지 않습니다.
        if (_status.text != "SPACE") _status.text = "SPACE";
    }

    private void OnDisable()
    {
        if (_dash != null) _dash.DashInputAttempted -= OnDashInput;
        _boundPlayer = null;
        _dash = null;
        if (_group != null) _group.alpha = 0;
    }
    private void OnDashInput(bool accepted) { if (_inputFeedback != null) _inputFeedback.Press(accepted); }
}

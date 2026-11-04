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
    [SerializeField] private Color _readyColor = new(.35f, .95f, 1f);
    [SerializeField] private Color _blockedColor = new(.55f, .63f, .66f);
    private PlayerSkillController _boundPlayer;
    private PlayerDash _dash;
    private int _displayedTenths = -1;
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
            _boundPlayer = _skillHud.Owner;
            _dash = _boundPlayer != null ? _boundPlayer.GetComponent<PlayerDash>() : null;
            _displayedTenths = -1;
        }
        bool visible = _dash != null && _dash.isActiveAndEnabled;
        _group.alpha = visible ? 1 : 0;
        if (!visible) return;
        _cooldownFill.fillAmount = _dash.CooldownProgress;
        _status.color = _dash.IsReady ? _readyColor : _blockedColor;
        int tenths = Mathf.CeilToInt(_dash.CooldownRemaining * 10);
        if (_displayedTenths != tenths)
        {
            _displayedTenths = tenths;
            _status.text = tenths > 0 ? $"{tenths / 10f:0.0}s" : "대시";
        }
    }

    private void OnDisable()
    {
        _boundPlayer = null;
        _dash = null;
        if (_group != null) _group.alpha = 0;
    }
}

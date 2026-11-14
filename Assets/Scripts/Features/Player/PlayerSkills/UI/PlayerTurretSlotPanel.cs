using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스킬 사용 시 세 포탑의 상태·체력을 표시하고 선택한 슬롯의 설치 프리뷰를 엽니다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(PlayerSkillHud))]
public sealed class PlayerTurretSlotPanel : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button[] _buttons;
    [SerializeField] private TMP_Text[] _statuses;
    [SerializeField] private Image[] _healthFills;
    [SerializeField] private Button _close;
    private PlayerSkillHud _hud;
    private PlayerSkillController _owner;
    private PlayerTurretSkill _skill;
    public bool IsConfigured => _panel != null && _close != null && _buttons != null && _buttons.Length == 3 &&
        _statuses != null && _statuses.Length == 3 && _healthFills != null && _healthFills.Length == 3 &&
        System.Array.TrueForAll(_buttons, b => b != null) && System.Array.TrueForAll(_statuses, t => t != null) &&
        System.Array.TrueForAll(_healthFills, i => i != null);

    private void Awake()
    {
        _hud = GetComponent<PlayerSkillHud>();
        if (!IsConfigured) { enabled = false; return; }
        for (int i = 0; i < _buttons.Length; i++)
        { int slot = i; _buttons[i].onClick.AddListener(() => Select(slot)); }
        _close.onClick.AddListener(Close);
        _panel.SetActive(false);
    }
    public bool Select(int slot)
    {
        bool selected = _skill != null && _skill.TrySelectSlot(slot);
        Refresh();
        return selected;
    }
    public void Close()
    {
        if (_skill != null && _skill.IsSelecting) _skill.CancelPlacement();
        if (_panel != null) _panel.SetActive(false);
    }
    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        if (!IsConfigured) return;
        var owner = _hud != null ? _hud.Owner : null;
        if (_owner != owner)
        {
            Close(); _owner = owner;
            _skill = owner != null ? owner.GetComponent<PlayerTurretSkill>() : null;
        }
        bool visible = _skill != null && _skill.isActiveAndEnabled && _skill.IsSelecting && _hud.IsFieldVisible && !_hud.IsOpen;
        _panel.SetActive(visible);
        if (!visible) return;
        for (int i = 0; i < _buttons.Length; i++)
        {
            bool exists = i < _skill.MaxTurretCount;
            _buttons[i].gameObject.SetActive(exists);
            if (!exists) continue;
            var slot = _skill.GetSlot(i);
            _buttons[i].interactable = _skill.CanSelectSlot(i);
            string state = slot.Status == PlayerTurretInventory.SlotStatus.Deployed ? "DEPLOYED"
                : slot.Status == PlayerTurretInventory.SlotStatus.Rebuilding ? $"REBUILD {Mathf.CeilToInt(slot.Remaining)}s"
                : _skill.IsCooldown ? $"WAIT {Mathf.CeilToInt(_skill.CurrentCooldownTimer)}s" : "READY";
            // 비율의 float 오차로 60%가 61%로 표시되지 않게 경계만 보정합니다.
            int percent = slot.HealthRatio > 0 ? Mathf.Clamp(Mathf.CeilToInt(slot.HealthRatio * 100 - .0001f), 1, 100) : 0;
            _statuses[i].text = state + "\nHP " + percent + "%";
            _healthFills[i].rectTransform.anchorMax = new Vector2(slot.HealthRatio, 1);
            _healthFills[i].color = slot.Status == PlayerTurretInventory.SlotStatus.Rebuilding
                ? new Color(1, .55f, .2f) : new Color(.35f, 1, .65f);
        }
    }
    private void OnDisable() { Close(); _owner = null; _skill = null; }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>프리팹에 배치된 무기 버튼의 선택 표시와 사용 가능 상태만 갱신합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponSlotView : MonoBehaviour
{
    [SerializeField, Range(1, PlayerWeaponSlots.Count)] private int _slotNumber = 1;
    [SerializeField] private Button _button;
    [SerializeField] private Image _border;
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _selectedMarker;
    [Header("State Colors")]
    [SerializeField] private Color _selectedColor = new(.35f, .95f, 1f);
    [SerializeField] private Color _idleColor = new(.22f, .39f, .43f);
    [SerializeField] private Color _unavailableIconColor = new(.4f, .5f, .55f);
    private PlayerWeaponHud _hud;

    public int SlotNumber => _slotNumber;
    public WeaponType Weapon
    {
        get { PlayerWeaponSlots.TryGetWeapon(_slotNumber, out var weapon); return weapon; }
    }
    public bool IsConfigured => PlayerWeaponSlots.TryGetWeapon(_slotNumber, out _) &&
        _button != null && _border != null && _icon != null && _icon.sprite != null && _selectedMarker != null;

    public void Bind(PlayerWeaponHud hud)
    {
        Unbind();
        _hud = hud;
        if (_button != null) _button.onClick.AddListener(SelectWeapon);
    }

    public void Unbind()
    {
        if (_button != null) _button.onClick.RemoveListener(SelectWeapon);
        _hud = null;
    }

    private void SelectWeapon() => _hud?.SelectSlot(_slotNumber);

    public void Refresh(WeaponType selected, bool canSelect, bool available)
    {
        bool equipped = selected == Weapon;
        _border.color = equipped ? _selectedColor : _idleColor;
        _selectedMarker.SetActive(equipped);
        _icon.color = available ? Color.white : _unavailableIconColor;
        _button.interactable = canSelect && available;
    }

    private void OnDisable() => Unbind();
}

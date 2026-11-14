using UnityEngine;

/// <summary>기존 씬 HUD의 로컬 소유자를 따라가며 미리 배치된 무기 슬롯을 연결합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class PlayerWeaponHud : MonoBehaviour
{
    [SerializeField] private PlayerSkillHud _skillHud;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private PlayerWeaponSlotView[] _slots;
    private PlayerSkillController _boundPlayer;
    private PlayerWeaponSelection _owner;
    public PlayerWeaponSelection Owner => _owner;

    public bool IsConfigured
    {
        get
        {
            if (_skillHud == null || _group == null || _slots == null || _slots.Length != PlayerWeaponSlotMapping.Count) return false;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] == null || !_slots[i].IsConfigured || _slots[i].SlotNumber != i + 1) return false;
            return true;
        }
    }

    private void OnEnable()
    {
        if (!IsConfigured)
        {
            Debug.LogError("[PlayerWeaponHud] Connect the scene Skill HUD, CanvasGroup and four authored weapon slots in order.", this);
            if (_group != null) _group.alpha = 0;
            return;
        }
        foreach (var slot in _slots) slot.Bind(this);
        Refresh();
    }

    private void LateUpdate()
    {
        if (!IsConfigured) return;
        var player = _skillHud.Owner;
        if (player != _boundPlayer)
        {
            UnbindOwner();
            _boundPlayer = player;
            _owner = player != null ? player.GetComponent<PlayerWeaponSelection>() : null;
            if (_owner != null) _owner.WeaponChanged += OnWeaponChanged;
        }
        Refresh();
    }

    public bool SelectSlot(int slot) => _owner != null && _owner.TrySelectWeaponSlot(slot);
    private void OnWeaponChanged(WeaponType weapon) => Refresh();

    private void Refresh()
    {
        if (_group == null) return;
        bool visible = _owner != null && _owner.isActiveAndEnabled && _skillHud.AreCombatControlsVisible;
        bool canSelect = visible && _owner.CanSelectWeapon;
        _group.alpha = visible ? 1f : 0f;
        _group.interactable = canSelect;
        _group.blocksRaycasts = canSelect;
        if (!visible) return;
        foreach (var slot in _slots)
            slot.Refresh(_owner.CurrentWeapon, canSelect, _owner.IsWeaponAvailable(slot.Weapon));
    }

    private void UnbindOwner()
    {
        if (_owner != null) _owner.WeaponChanged -= OnWeaponChanged;
        _owner = null;
        _boundPlayer = null;
    }

    private void OnDisable()
    {
        UnbindOwner();
        if (_slots != null)
            foreach (var slot in _slots) if (slot != null) slot.Unbind();
        if (_group != null) _group.alpha = 0;
    }
}

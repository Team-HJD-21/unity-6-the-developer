using System;
using UnityEngine;

/// <summary>현재 무기와 숫자키·휠·Tab 선택을 관리하고, 장착 외형과 변경 알림을 갱신합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler))]
// 입력 수집(-200)과 스킬 상태 결정(100) 이후, 공격 LateUpdate(300) 전에 선택을 반영합니다.
[DefaultExecutionOrder(200)]
public sealed class PlayerWeaponSelection : MonoBehaviour
{
    [Header("Equipment")]
    [SerializeField] private WeaponType _currentWeapon = WeaponType.DefaultGun;
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private GameObject _sniperBulletPrefab;
    [SerializeField] private PlayerSpannerWeapon _spannerPrefab;
    [Header("References")]
    [SerializeField] private PlayerInputHandler _input;
    [SerializeField] private PlayerWeaponRig _weaponRig;
    private PlayerSpannerWeapon _spanner;
    private PlayerControlState _control;
    private PlayerSkillController _skills;

    public WeaponType CurrentWeapon => _currentWeapon;
    public event Action<WeaponType> WeaponChanged;
    public bool CanSelectWeapon => isActiveAndEnabled && Time.timeScale > 0 &&
        (_control == null || _control.CanAct) && (_skills == null || _skills.CanUse);
    public GameObject CurrentBulletPrefab => _currentWeapon == WeaponType.Sniper ? _sniperBulletPrefab : _bulletPrefab;
    public PlayerSpannerWeapon Spanner => _spanner;

    private void Awake()
    {
        if (_input == null) _input = GetComponent<PlayerInputHandler>();
        if (_weaponRig == null) _weaponRig = GetComponentInChildren<PlayerWeaponRig>(true);
        _control = GetComponent<PlayerControlState>();
        _skills = GetComponent<PlayerSkillController>();
        if (_spannerPrefab != null)
        {
            _spanner = Instantiate(_spannerPrefab, transform);
            _spanner.Bind(transform, _input);
        }
        RefreshEquippedObjects();
    }

    private void Update()
    {
        if (_input == null || !CanSelectWeapon) return;
        if (_input.RequestedWeaponSlot > 0) TrySelectWeaponSlot(_input.RequestedWeaponSlot);
        else if (_input.WeaponScrollStep != 0) SelectAdjacentWeapon(_input.WeaponScrollStep);
        else if (_input.WasWeaponSwitchPressed) SelectNextWeapon();
    }

    public bool IsWeaponAvailable(WeaponType weapon) => weapon switch
    {
        WeaponType.Spanner => _spanner != null,
        WeaponType.DefaultGun or WeaponType.CryoBlaster => _bulletPrefab != null,
        WeaponType.Sniper => _sniperBulletPrefab != null,
        _ => false
    };

    public bool TrySelectWeaponSlot(int slot) =>
        PlayerWeaponSlotMapping.TryGetWeapon(slot, out var weapon) && TrySelectWeapon(weapon);

    public bool TrySelectWeapon(WeaponType weapon)
    {
        if (!CanSelectWeapon || !IsWeaponAvailable(weapon)) return false;
        if (_currentWeapon == weapon) return true;
        _currentWeapon = weapon;
        RefreshEquippedObjects();
        WeaponChanged?.Invoke(weapon);
        return true;
    }

    public void SelectNextWeapon() => SelectAdjacentWeapon(1);

    public bool SelectAdjacentWeapon(int direction)
    {
        if (!CanSelectWeapon || direction == 0) return false;
        int slot = PlayerWeaponSlotMapping.GetSlot(_currentWeapon);
        int step = direction > 0 ? 1 : -1;
        if (slot == 0) slot = step > 0 ? 0 : 1;
        for (int attempt = 0; attempt < PlayerWeaponSlotMapping.Count; attempt++)
        {
            slot = (slot - 1 + step + PlayerWeaponSlotMapping.Count) % PlayerWeaponSlotMapping.Count + 1;
            if (PlayerWeaponSlotMapping.TryGetWeapon(slot, out var candidate) && IsWeaponAvailable(candidate))
                return TrySelectWeapon(candidate);
        }
        return false;
    }

    private void RefreshEquippedObjects()
    {
        bool melee = _currentWeapon == WeaponType.Spanner && _spanner != null;
        if (_weaponRig != null) _weaponRig.SetRangedWeaponVisible(!melee);
        if (_spanner != null) _spanner.gameObject.SetActive(melee);
    }
}

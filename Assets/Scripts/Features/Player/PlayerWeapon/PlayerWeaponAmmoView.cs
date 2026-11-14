using TMPro;
using UnityEngine;

/// <summary>Weapon Hotbar 왼쪽에 미리 배치한 잔탄·재장전 표시를 현재 로컬 Player에 연결합니다.</summary>
[DisallowMultipleComponent]
// 공격 LateUpdate(300)의 마지막 탄환 소비·자동 재장전을 같은 화면 프레임에 반영합니다.
[DefaultExecutionOrder(400)]
public sealed class PlayerWeaponAmmoView : MonoBehaviour
{
    [SerializeField] private PlayerWeaponHud _weaponHud;
    [SerializeField] private TMP_Text _ammoLabel;
    [SerializeField] private GameObject _reloadIndicator;
    [SerializeField] private RectTransform _reloadArrows;
    [SerializeField] private TMP_Text _reloadLabel;
    [SerializeField] private Color _normalColor = new(.08f, .12f, .15f);
    [SerializeField] private Color _emptyColor = new(.7f, .12f, .08f);
    private PlayerWeaponSelection _owner;
    private PlayerWeaponAmmo _ammo;
    private int _lastRounds = -1, _lastCapacity = -1;
    private bool _wasInfinite, _wasPercentage;

    private void LateUpdate()
    {
        var owner = _weaponHud != null ? _weaponHud.Owner : null;
        if (_owner != owner)
        {
            _owner = owner;
            _ammo = owner != null ? owner.GetComponent<PlayerWeaponAmmo>() : null;
            _lastRounds = -1;
        }
        if (_ammoLabel == null) return;
        if (_ammo == null) { _ammoLabel.text = ""; SetReloadVisible(false); return; }
        int rounds = _ammo.CurrentRounds, capacity = _ammo.CurrentCapacity;
        if (rounds != _lastRounds || capacity != _lastCapacity || _wasInfinite != _ammo.IsInfinite || _wasPercentage != _ammo.UsesPercentage)
        {
            _ammoLabel.text = _ammo.IsInfinite ? "∞" : _ammo.UsesPercentage ? $"{_ammo.CurrentPercentage}%" : $"{rounds}/{capacity}";
            _ammoLabel.color = !_ammo.IsInfinite && rounds == 0 ? _emptyColor : _normalColor;
            _lastRounds = rounds; _lastCapacity = capacity; _wasInfinite = _ammo.IsInfinite;
            _wasPercentage = _ammo.UsesPercentage;
        }
        SetReloadVisible(_ammo.IsReloading);
        if (_ammo.IsReloading && _reloadArrows != null)
            _reloadArrows.localRotation = Quaternion.Euler(0, 0, -720f * _ammo.ReloadProgress);
    }

    private void SetReloadVisible(bool visible)
    {
        if (_ammoLabel != null) _ammoLabel.enabled = !visible;
        if (_reloadIndicator != null && _reloadIndicator.activeSelf != visible) _reloadIndicator.SetActive(visible);
        if (_reloadLabel != null) _reloadLabel.enabled = visible;
    }
}

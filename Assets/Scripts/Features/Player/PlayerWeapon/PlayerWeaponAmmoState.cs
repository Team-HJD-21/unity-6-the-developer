using System;

/// <summary>총기별 탄창과 한 번의 재장전 진행을 보관합니다. 시간은 필드 시계에서 전달합니다.</summary>
public sealed partial class PlayerWeaponAmmoState
{
    private readonly int[] _capacities;
    private readonly int[] _rounds;
    private WeaponType _reloadWeapon;
    private double _reloadRemaining;
    private double _reloadDuration;

    public bool IsReloading => _reloadRemaining > 0;
    public WeaponType ReloadWeapon => _reloadWeapon;
    public float ReloadProgress => IsReloading ? (float)(1 - _reloadRemaining / _reloadDuration) : 0f;

    public PlayerWeaponAmmoState(int defaultGunCapacity = 15, int cryoCapacity = 15, int sniperCapacity = 5)
    {
        if (defaultGunCapacity <= 0 || cryoCapacity <= 0 || sniperCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(defaultGunCapacity));
        _capacities = new[] { defaultGunCapacity, cryoCapacity, 0, sniperCapacity };
        _rounds = (int[])_capacities.Clone();
    }

    public int GetCapacity(WeaponType weapon) => _capacities[Index(weapon)];
    public int GetRounds(WeaponType weapon) => _rounds[Index(weapon)];
    public bool CanFire(WeaponType weapon) => !IsReloading &&
        (weapon == WeaponType.Spanner || GetRounds(weapon) > 0);

    /// <summary>산탄 수와 무관하게 실제 발사 성공 한 번에 한 발만 소비합니다.</summary>
    public bool TryConsumeRound(WeaponType weapon)
    {
        if (!CanFire(weapon)) return false;
        if (weapon != WeaponType.Spanner) _rounds[Index(weapon)]--;
        return true;
    }

    public bool TryStartReload(WeaponType weapon, double duration)
    {
        if (weapon == WeaponType.Spanner || IsReloading || !IsFinite(duration) || duration <= 0 ||
            GetRounds(weapon) >= GetCapacity(weapon)) return false;
        _reloadWeapon = weapon;
        _reloadDuration = _reloadRemaining = duration;
        return true;
    }

    public void AdvanceReload(double deltaTime)
    {
        if (!IsReloading || !IsFinite(deltaTime) || deltaTime <= 0) return;
        _reloadRemaining = Math.Max(0, _reloadRemaining - deltaTime);
        if (IsReloading) return;
        _rounds[Index(_reloadWeapon)] = GetCapacity(_reloadWeapon);
        CancelReload();
    }

    public void CancelReload() { _reloadRemaining = 0; _reloadDuration = 0; }

    private static int Index(WeaponType weapon)
    {
        int index = (int)weapon;
        if (index < 0 || index > (int)WeaponType.Sniper) throw new ArgumentOutOfRangeException(nameof(weapon));
        return index;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

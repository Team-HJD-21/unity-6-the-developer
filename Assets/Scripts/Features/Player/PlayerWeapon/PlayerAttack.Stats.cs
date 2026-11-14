using UnityEngine;

/// <summary>실제 공격 설정에서 읽은 UI용 값입니다. 피해량은 탄환 한 발·분사 한 틱·근접 타격 한 번 기준입니다.</summary>
public readonly struct PlayerWeaponStats
{
    public readonly bool IsMelee;
    public readonly bool IsSpray;
    public readonly float Damage, Interval, ProjectileSpeed, Range;
    public readonly int ProjectileCount;
    public PlayerWeaponStats(bool isMelee, float damage, float interval, int projectileCount, float speed, float range, bool isSpray = false)
    {
        IsMelee = isMelee; IsSpray = isSpray; Damage = damage; Interval = interval;
        ProjectileCount = projectileCount; ProjectileSpeed = speed; Range = range;
    }
}

public partial class PlayerAttack
{
    /// <summary>Inspector 수치를 UI에 복제하지 않고 현재 무기의 실제 공격 설정과 탄환 프리팹을 조회합니다.</summary>
    public bool TryGetCurrentWeaponStats(out PlayerWeaponStats stats)
    {
        stats = default;
        if (_selection == null) return false;
        if (_selection.CurrentWeapon == WeaponType.Spanner)
        {
            var spanner = _selection.Spanner;
            if (spanner == null) return false;
            stats = new PlayerWeaponStats(true, _baseStats != null ? _baseStats.ScaleWeaponDamage(spanner.AttackDamage) : spanner.AttackDamage,
                spanner.AttackInterval, 0, 0, spanner.AttackRange);
            return true;
        }
        var shot = GetShotSettings();
        if (shot.cryogenic)
        {
            if (_cryoSpray == null || !_cryoSpray.IsConfigured) return false;
            stats = new PlayerWeaponStats(false, _baseStats != null ? _baseStats.ScaleWeaponDamage(shot.damage) : Mathf.Max(0, shot.damage),
                Mathf.Max(.01f, shot.interval), 0, 0, _cryoSpray.Range, true);
            return true;
        }
        var bullet = CurrentBulletPrefab != null ? CurrentBulletPrefab.GetComponent<PlayerBullet>() : null;
        if (bullet == null) return false;
        stats = new PlayerWeaponStats(false, _baseStats != null ? _baseStats.ScaleWeaponDamage(shot.damage) : Mathf.Max(0, shot.damage), Mathf.Max(.01f, shot.interval),
            Mathf.Clamp(shot.pelletCount, 1, 32), bullet.FlightSpeed,
            bullet.FlightSpeed * bullet.FlightLifetime);
        return true;
    }
}

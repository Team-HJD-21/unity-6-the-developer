using UnityEngine;

/// <summary>보조 포탑의 실제 총구에서 전용 탄환을 생성합니다.</summary>
public static class PlayerSupportTurretWeapon
{
    public static TemporaryTurretProjectile Fire(GameObject bulletPrefab, Transform targetEnemy, Transform firePoint, Transform turretHead, float damagePerShot, Color bulletColor)
    {
        if (bulletPrefab == null || targetEnemy == null || firePoint == null ||
            turretHead == null || !firePoint.IsChildOf(turretHead) ||
            bulletPrefab.GetComponent<TemporaryTurretProjectile>() == null) return null;

        Vector2 dir = (targetEnemy.position - firePoint.position).normalized;
        if (!PlayerAimUtility.IsValidDirection(dir)) return null;
        GameObject bulletObj = Object.Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        PlayerFieldSimulationUtility.BindSpawned(bulletObj, firePoint);
        var bullet = bulletObj.GetComponent<TemporaryTurretProjectile>();
        if (!bullet.Initialize(dir, damagePerShot)) { PlayerFieldDevice.Dispose(bulletObj); return null; }
        var renderer = bulletObj.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = bulletColor;
        return bullet;
    }
}

using UnityEngine;

/// <summary>플레이어 터렛의 실제 총구에서 전용 탄환을 생성합니다.</summary>
public static class PlayerTurretWeapon
{
    public static void Fire(GameObject bulletPrefab, Transform targetEnemy, Transform firePoint, Transform turretHead, float damagePerShot, Color bulletColor)
    {
        if (bulletPrefab == null || targetEnemy == null || firePoint == null ||
            turretHead == null || !firePoint.IsChildOf(turretHead) ||
            bulletPrefab.GetComponent<PlayerTurretProjectile>() == null) return;

        Vector2 dir = (targetEnemy.position - firePoint.position).normalized;
        if (!PlayerAimUtility.IsValidDirection(dir)) return;
        GameObject bulletObj = Object.Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        var bullet = bulletObj.GetComponent<PlayerTurretProjectile>();
        if (!bullet.Initialize(dir, damagePerShot)) { PlayerFieldDevice.Dispose(bulletObj); return; }
        var renderer = bulletObj.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = bulletColor;
    }
}

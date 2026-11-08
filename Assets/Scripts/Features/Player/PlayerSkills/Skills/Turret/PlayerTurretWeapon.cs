// Player 소형 터렛의 표적·발사 주기와 소유 투사체를 같은 필드 시간으로 관리합니다.
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>플레이어 터렛의 실제 총구에서 전용 탄환을 생성합니다.</summary>
public static class PlayerTurretWeapon
{
    public static PlayerTurretProjectile Fire(GameObject bulletPrefab, Transform targetEnemy, Transform firePoint, Transform turretHead, float damagePerShot, Color bulletColor)
    {
        if (bulletPrefab == null || targetEnemy == null || firePoint == null ||
            turretHead == null || !firePoint.IsChildOf(turretHead) ||
            bulletPrefab.GetComponent<PlayerTurretProjectile>() == null) return null;

        Vector2 dir = (targetEnemy.position - firePoint.position).normalized;
        if (!PlayerAimUtility.IsValidDirection(dir)) return null;
        GameObject bulletObj = Object.Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        FieldSimulationTime.BindSpawned(bulletObj, firePoint);
        var bullet = bulletObj.GetComponent<PlayerTurretProjectile>();
        if (!bullet.Initialize(dir, damagePerShot)) { PlayerFieldDevice.Dispose(bulletObj); return null; }
        var renderer = bulletObj.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = bulletColor;
        return bullet;
    }
}

using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class EnemyProjectileSpawner : NetworkBehaviour
{
    public bool TrySpawn(
        EnemyDamageProjectile prefab,
        Vector3 position,
        Vector2 direction,
        int damage)
    {
        EnemyDamageProjectile projectile = CreateProjectile(prefab, position);
        if (projectile == null)
            return false;

        projectile.Initialize(damage, direction);
        SpawnNetworkProjectile(projectile);

        return true;
    }

    public bool TrySpawn(
        HealingOrbProjectile prefab,
        Vector3 position,
        EnemyHealth target,
        int healAmount)
    {
        if (target == null ||
            !target.CanBeHealed ||
            healAmount <= 0)
        {
            return false;
        }

        HealingOrbProjectile projectile = CreateProjectile(prefab, position);
        if (projectile == null)
            return false;

        projectile.Initialize(target, healAmount);
        SpawnNetworkProjectile(projectile);

        return true;
    }

    /// <summary>실행 권한을 확인하고 초기화할 투사체를 생성한다.</summary>
    private T CreateProjectile<T>(T prefab, Vector3 position) where T : EnemyProjectile
    {
        if (prefab == null)
            return null;

        var manager = Unity.Netcode.NetworkManager.Singleton;
        bool networkRunning = manager != null && manager.IsListening;

        if (networkRunning && (!IsSpawned || !IsServer))
            return null;

        return Instantiate(prefab, position, Quaternion.identity);
    }

    /// <summary>초기화한 투사체를 네트워크에 등록한다.</summary>
    private static void SpawnNetworkProjectile(EnemyProjectile projectile)
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
            projectile.NetworkObject.Spawn();
    }
}

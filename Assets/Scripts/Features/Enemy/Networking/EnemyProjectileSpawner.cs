using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class EnemyProjectileSpawner : NetworkBehaviour
{
    public bool TrySpawn(
        EnemyProjectile prefab,
        Vector3 position,
        Vector2 direction,
        int damage)
    {
        if (prefab == null)
            return false;

        var manager = Unity.Netcode.NetworkManager.Singleton;
        bool networkRunning = manager != null && manager.IsListening;

        if (networkRunning && (!IsSpawned || !IsServer))
            return false;

        EnemyProjectile projectile =
            Instantiate(prefab, position, Quaternion.identity);
        
        projectile.Initialize(damage, direction);

        if (networkRunning)
            projectile.NetworkObject.Spawn();

        return true;
    }
        
}

// 서버의 적 공격이 만든 탄환에 생성자의 Match 시간과 수명을 전달합니다.
using TeamHJD.Game.Presentation.Scene;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class EnemyProjectileSpawner : NetworkBehaviour
{
    public bool TrySpawn(
        EnemyDamageProjectile prefab,
        Vector3 position,
        Vector2 direction,
        int damage, Component owner = null)
    {
        EnemyDamageProjectile projectile = CreateProjectile(prefab, position, owner);
        if (projectile == null) return false;

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

        HealingOrbProjectile projectile = CreateProjectile(prefab, position, this);
        if (projectile == null)
            return false;

        projectile.Initialize(target, healAmount);
        SpawnNetworkProjectile(projectile);

        return true;
    }

    /// <summary>실행 권한을 확인하고 초기화할 투사체를 생성한다.</summary>
    private T CreateProjectile<T>(T prefab, Vector3 position, Component owner) where T : EnemyProjectile
    {
        if (prefab == null)
            return null;

        var manager = Unity.Netcode.NetworkManager.Singleton;
        bool networkRunning = manager != null && manager.IsListening;

        if (networkRunning && (!IsSpawned || !IsServer))
            return null;

        Component source = owner != null ? owner : this;
        if (source is Behaviour behaviour && !behaviour.isActiveAndEnabled) return null;
        if (!FieldSimulationTime.CanAdvance(source)) return null;

        T projectile = Instantiate(prefab, position, Quaternion.identity);
        projectile.BindOwner(source, destroyOnCompletion: !networkRunning);
        return projectile;
    }

    /// <summary>초기화한 투사체를 네트워크에 등록한다.</summary>
    private static void SpawnNetworkProjectile(EnemyProjectile projectile)
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
            projectile.NetworkObject.Spawn();
    }
}

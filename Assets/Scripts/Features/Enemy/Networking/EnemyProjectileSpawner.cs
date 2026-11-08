// 서버의 적 공격이 만든 탄환에 생성자의 Match 시간과 수명을 전달합니다.
using TeamHJD.Game.Presentation.Scene;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class EnemyProjectileSpawner : NetworkBehaviour
{
    public bool TrySpawn(
        EnemyProjectile prefab,
        Vector3 position,
        Vector2 direction,
        int damage, Component owner = null)
    {
        if (prefab == null)
            return false;
        Component source = owner != null ? owner : this;
        if (source is Behaviour behaviour && !behaviour.isActiveAndEnabled)
            return false;
        if (!FieldSimulationTime.CanAdvance(source))
            return false;

        var manager = Unity.Netcode.NetworkManager.Singleton;
        bool networkRunning = manager != null && manager.IsListening;

        if (networkRunning && (!IsSpawned || !IsServer))
            return false;

        EnemyProjectile projectile =
            Instantiate(prefab, position, Quaternion.identity);

        projectile.BindOwner(source, destroyOnCompletion: !networkRunning);
        projectile.Initialize(damage, direction);

        if (networkRunning)
            projectile.NetworkObject.Spawn();

        return true;
    }

}

using Unity.Netcode;
using UnityEngine;

public class EnemyProjectileWithImpactEffect : EnemyDamageProjectile
{
    [SerializeField] private GameObject explosionEffectPrefab;
    
    
    protected override void PlayImpactEffect(Vector3 position)
    {
        if (IsSpawned)
            PlayExplosionRpc(position);
        else
            PlayExplosion(position);
    }
    
    [Rpc(
        SendTo.ClientsAndHost,
        InvokePermission = RpcInvokePermission.Server)]
    private void PlayExplosionRpc(Vector3 position)
    {
        PlayExplosion(position);
    }

    private void PlayExplosion(Vector3 position)
    {
        if (explosionEffectPrefab == null)
            return;

        Instantiate(
            explosionEffectPrefab,
            position,
            Quaternion.identity);
    }
}

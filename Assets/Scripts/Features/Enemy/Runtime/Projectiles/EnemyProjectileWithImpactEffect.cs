using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 명중 효과 프리팹을 로컬 또는 네트워크 수신 위치에서 재생한다.
/// </summary>
public class EnemyProjectileWithImpactEffect : EnemyDamageProjectile
{
    [SerializeField] private GameObject explosionEffectPrefab;
    
    
    /// <summary>
    /// 네트워크 실행 중에는 RPC로 모든 수신 측에 명중 효과를 재생한다.
    /// </summary>
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

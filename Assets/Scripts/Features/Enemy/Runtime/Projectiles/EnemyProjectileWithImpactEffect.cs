using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 명중 효과 프리팹을 로컬 또는 네트워크 수신 위치에서 재생한다.
/// </summary>
public class EnemyProjectileWithImpactEffect : EnemyDamageProjectile
{
    [SerializeField] private GameObject explosionEffectPrefab;

    // 파생 클래스 공통 처리

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

    // 내부 처리

    /// <summary>서버가 전달한 명중 위치에서 수신 측의 효과를 재생한다.</summary>
    [Rpc(
        SendTo.ClientsAndHost,
        InvokePermission = RpcInvokePermission.Server)]
    private void PlayExplosionRpc(Vector3 position)
    {
        PlayExplosion(position);
    }

    /// <summary>효과 프리팹이 연결되어 있으면 지정 위치에 생성한다.</summary>
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

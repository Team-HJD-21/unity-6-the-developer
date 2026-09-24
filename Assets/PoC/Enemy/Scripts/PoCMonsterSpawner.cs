using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버에서 테스트용 몬스터를 생성하고 임시 타겟을 지정한다.
/// </summary>
public class PoCMonsterSpawner : NetworkBehaviour
{
    [Header("생성 설정")]
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("타겟 설정")]
    [SerializeField] private Transform tempTarget;

    /// <summary>
    /// 서버에서 몬스터를 생성한 뒤 모든 클라이언트에 스폰한다.
    /// </summary>
    public void Spawn()
    {
        if (!IsServer)
            return;

        GameObject spawnedMonster = Instantiate(
            monsterPrefab,
            spawnPoint.position,
            spawnPoint.rotation);

        NetworkObject networkObject = spawnedMonster.GetComponent<NetworkObject>();
        networkObject.Spawn();

        Debug.Log($"Spawned test monster. NetworkObjectId: {networkObject.NetworkObjectId}");
    }
}

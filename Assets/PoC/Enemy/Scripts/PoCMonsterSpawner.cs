using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 전달받은 생성 명령을 서버에서 실행하는 PoC용 스폰 실행기다.
/// 현재는 임시 프리팹과 위치를 사용하며, 생성된 몬스터를 모든 클라이언트에 동기화한다.
/// </summary>
public class PoCMonsterSpawner : NetworkBehaviour
{
    [Header("임시 생성")]
    [SerializeField] private GameObject tempMonsterPrefab;
    [SerializeField] private Transform tempSpawnPoint;

    /// <summary>
    /// 전달받은 명령을 검증하고 서버에서 테스트 몬스터를 생성한다.
    /// </summary>
    /// <param name="spawnInstruction">생성할 적과 위치, 수량 정보가 담긴 명령.</param>
    public void Execute(SpawnInstruction spawnInstruction)
    {
        // 네트워크 오브젝트 생성은 서버에서만 수행한다.
        if (!IsServer || spawnInstruction.Count < 0)
            return;

        // [임시] 등록된 프리팹이 없으면 생성하지 않는다.
        if (tempMonsterPrefab == null || tempSpawnPoint == null)
            return;

        // 임시 프리팹을 지정된 테스트 위치에 생성한다.
        GameObject spawnedMonster = Instantiate(
            tempMonsterPrefab,
            tempSpawnPoint.position,
            tempSpawnPoint.rotation);

        // 생성된 몬스터를 NGO에 등록해 모든 클라이언트에 전달한다.
        NetworkObject networkObject = spawnedMonster.GetComponent<NetworkObject>();
        networkObject.Spawn();

        // 테스트 중 생성된 위치를 확인하기 위해 로그를 남긴다.
        Debug.Log($"Spawned test monster: {tempSpawnPoint.name}");
    }
}

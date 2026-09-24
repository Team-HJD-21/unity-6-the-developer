using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버에서 테스트용 몬스터 유형 하나를 무작위로 생성한다.
/// </summary>
public class PoCMonsterSpawner : NetworkBehaviour
{
    [Header("생성 설정")]
    [SerializeField] private List<GameObject> monsterPrefabs;
    [SerializeField] private Transform spawnPoint;

    /// <summary>
    /// 서버에서 몬스터를 생성한 뒤 모든 클라이언트에 스폰한다.
    /// </summary>
    public void Spawn()
    {
        if (!IsServer)
            return;

        // 등록된 프리팹이 없으면 생성하지 않는다.
        if (monsterPrefabs == null || monsterPrefabs.Count == 0)
            return;

        // 생성할 몬스터 유형을 무작위로 선택한다.
        GameObject selectedPrefab =
            monsterPrefabs[Random.Range(0, monsterPrefabs.Count)];

        GameObject spawnedMonster = Instantiate(
            selectedPrefab,
            spawnPoint.position,
            spawnPoint.rotation);

        NetworkObject networkObject = spawnedMonster.GetComponent<NetworkObject>();
        networkObject.Spawn();

        // 무작위로 선택된 AI 유형을 테스트 중 확인하기 위해 유지한다.
        Debug.Log($"Spawned test monster: {selectedPrefab.name}");
    }
}
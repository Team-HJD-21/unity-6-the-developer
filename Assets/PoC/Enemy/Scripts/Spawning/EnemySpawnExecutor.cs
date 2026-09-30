using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 전달받은 생성 명령을 서버에서 실행하는 적 스폰 실행기다.
/// 적 정의와 스폰 지점을 조회하고 생성된 적을 모든 클라이언트에 동기화한다.
/// </summary>
public class EnemySpawnExecutor : NetworkBehaviour
{
    [SerializeField] private EnemyCatalog enemyList;
    [SerializeField] private SpawnPointRegistry spawnPointList;
    
    
    /// <summary>
    /// 전달받은 명령을 검증하고 서버에서 테스트 몬스터를 생성한다.
    /// </summary>
    /// <param name="spawnInstruction">생성할 적과 위치, 수량 정보가 담긴 명령.</param>
    public void Execute(SpawnInstruction spawnInstruction)
    {
        // 네트워크 오브젝트 생성은 서버에서만 수행한다.
        if (!IsServer || spawnInstruction.Count <= 0)
            return;

        if (enemyList == null || spawnPointList == null)
        {
            Debug.LogError("Spawner dependencies are not assigned.");
            return;
        }

        
        if (!enemyList.TryGetDefinition(
                spawnInstruction.EnemyId,
                out EnemyDefinition enemy))
        {
            Debug.LogError($"Enemy not found: {spawnInstruction.EnemyId}");
            return;
        }

        if (!spawnPointList.TryGetSpawnPoint(
                spawnInstruction.SpawnPointId,
                out SpawnPoint spawnPoint))
        {
            Debug.LogError($"SpawnPoint not found: {spawnInstruction.SpawnPointId}");
            return;
        }

        if (enemy.Prefab == null ||
            !enemy.Prefab.TryGetComponent<NetworkObject>(out _))
        {
            Debug.LogError(
                $"Enemy prefab requires NetworkObject: {enemy.EnemyId}");
            return;
        }

        for (int i = 0; i < spawnInstruction.Count; i++)
        {
            
            Vector2 offset = Random.insideUnitCircle * spawnPoint.SpawnRadius;

            Vector3 spawnPosition =
                spawnPoint.Position + new Vector3(offset.x, offset.y, 0f);
            
            GameObject spawnedMonster = Instantiate(
                enemy.Prefab,
                spawnPosition,
                spawnPoint.Rotation);

            // 생성된 몬스터를 NGO에 등록해 모든 클라이언트에 전달한다.
            NetworkObject networkObject = spawnedMonster.GetComponent<NetworkObject>();
            networkObject.Spawn();
            
            
        }
    }
}

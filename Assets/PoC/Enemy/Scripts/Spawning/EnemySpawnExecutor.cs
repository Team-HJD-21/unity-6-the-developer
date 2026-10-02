using System.Collections.Generic;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Planner가 확정한 분대 생성 명령을 서버에서 실행한다.
/// 스폰 지점과 적 프리팹을 조회한 뒤 각 적을 생성하고 NGO를 통해 클라이언트에 동기화한다.
/// 생성 수나 프리셋 구성은 이 계층에서 다시 결정하지 않는다.
/// </summary>
public class EnemySpawnExecutor : NetworkBehaviour
{
    [SerializeField] private EnemyCatalog enemyList;
    private SpawnPointRegistry _spawnPointList = new();

    /// <summary>
    /// 자식 오브젝트에 등록된 스폰 지점과 현재 점령 상태를 조회할 레지스트리다.
    /// </summary>
    public SpawnPointRegistry SpawnPointList => _spawnPointList;

    /// <summary>
    /// 자식 SpawnPoint를 수집해 Planner와 Executor가 같은 지점 목록을 사용하게 한다.
    /// </summary>
    void Awake()
    {
        SpawnPoint[] points = GetComponentsInChildren<SpawnPoint>();
        _spawnPointList.Initialize(points);
    }
    
    /// <summary>
    /// 명령에 포함된 모든 적 정의를 검증한 뒤 서버에서 적별 수량만큼 생성한다.
    /// 실행 전 적 정의와 NetworkObject를 모두 검증해 일부 종류만 생성되는 일을 막는다.
    /// 계획 후 점령 상태가 바뀐 지점에서는 생성하지 않는다.
    /// </summary>
    /// <param name="spawnInstruction">분대의 스폰 지점과 적별 수량이 담긴 명령.</param>
    public void Execute(SpawnInstruction spawnInstruction)
    {
        // 네트워크 오브젝트 생성은 서버에서만 수행한다.
        if (!IsServer || spawnInstruction.Enemies == null ||
            spawnInstruction.Enemies.Count == 0)
            return;

        if (enemyList == null || _spawnPointList == null)
        {
            Debug.LogError("Spawner dependencies are not assigned.");
            return;
        }

        // 분대 내 모든 적은 같은 스폰 지점을 공유하므로 한 번만 조회한다.
        if (!_spawnPointList.TryGetSpawnPoint(
                spawnInstruction.SpawnPointId,
                out SpawnPoint spawnPoint))
        {
            Debug.LogError($"SpawnPoint not found: {spawnInstruction.SpawnPointId}");
            return;
        }

        // 계획 이후 점령 상태가 바뀐 지점에서는 적을 생성하지 않는다.
        if (!spawnPoint.CanSpawnEnemies)
            return;

        // 적 종류를 순서대로 조회하고 프리팹의 NetworkObject까지 확인한 후 생성한다.
        List<EnemyDefinition> definitions = new(spawnInstruction.Enemies.Count);
        foreach (EnemySpawnEntry entry in spawnInstruction.Enemies)
        {
            if (string.IsNullOrWhiteSpace(entry.EnemyId) || entry.Count <= 0 ||
                !enemyList.TryGetDefinition(entry.EnemyId, out EnemyDefinition enemy))
            {
                Debug.LogError($"Invalid enemy spawn entry: {entry.EnemyId}");
                return;
            }

            if (enemy.Prefab == null ||
                !enemy.Prefab.TryGetComponent<NetworkObject>(out _))
            {
                Debug.LogError(
                    $"Enemy prefab requires NetworkObject: {enemy.EnemyId}");
                return;
            }

            definitions.Add(enemy);
        }

        for (int entryIndex = 0; entryIndex < spawnInstruction.Enemies.Count; entryIndex++)
        {
            EnemyDefinition enemy = definitions[entryIndex];
            int count = spawnInstruction.Enemies[entryIndex].Count;

            for (int i = 0; i < count; i++)
            {
                // 한 지점에서 겹쳐 생성되지 않도록 설정된 반지름 안에 위치를 분산한다.
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
}

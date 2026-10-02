using System.Collections.Generic;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Planner가 확정한 SpawnInstruction 하나를 서버에서 하나의 분대로 생성한다.
/// 스폰 지점과 적 프리팹을 확인하고, 생성된 적을 분대에 등록한 뒤 NGO로 동기화한다.
/// 생성 수와 프리셋 구성은 다시 결정하지 않는다.
/// </summary>
public class EnemySpawnExecutor : NetworkBehaviour
{
    [SerializeField] private EnemyCatalog enemyList;
    private SpawnPointRegistry _spawnPointList = new();
    private readonly List<EnemySquad> _squads = new();

    /// <summary>
    /// Planner와 Executor가 함께 사용하는 스폰 지점 및 점령 상태 목록을 반환한다.
    /// </summary>
    public SpawnPointRegistry SpawnPointList => _spawnPointList;

    /// <summary>
    /// 자식 SpawnPoint를 수집해 생성 전 확인과 Planner의 계획에 사용할 목록을 초기화한다.
    /// </summary>
    void Awake()
    {
        SpawnPoint[] points = GetComponentsInChildren<SpawnPoint>();
        _spawnPointList.Initialize(points);
    }

    /// <summary>
    /// 서버에서 생성된 각 분대의 목표를 갱신한다.
    /// 유효한 목표가 유지되는 분대는 후보 검색 없이 넘어간다.
    /// </summary>
    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;

        foreach (EnemySquad squad in _squads)
            squad.SetTarget();
    }

    /// <summary>
    /// Executor의 네트워크 수명이 끝나면 보관 중인 분대 참조를 비운다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _squads.Clear();
        base.OnNetworkDespawn();
    }
    
    /// <summary>
    /// 서버에서 분대 생성 명령의 지점·수량·프리팹을 검증하고 적을 생성한다.
    /// 구성원을 같은 분대에 등록한 뒤 네트워크에 스폰하고 공동 목표를 지정한다.
    /// 계획 후 지점이 점령되어 생성할 수 없게 되었다면 명령을 실행하지 않는다.
    /// </summary>
    /// <param name="spawnInstruction">분대의 스폰 지점과 적별 수량이 담긴 명령.</param>
    public void Execute(SpawnInstruction spawnInstruction)
    {
        // NGO 스폰은 서버에서만 수행하며, 적 구성 항목이 없는 명령은 무시한다.
        if (!IsServer || spawnInstruction.Enemies == null ||
            spawnInstruction.Enemies.Count == 0)
            return;

        if (enemyList == null || _spawnPointList == null)
        {
            Debug.LogError("Spawner dependencies are not assigned.");
            return;
        }

        // 명령 하나가 분대 하나이므로 모든 구성원이 공유할 스폰 지점을 한 번만 찾는다.
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

        // 생성 전에 모든 적 정의와 필수 컴포넌트를 확인해 일부 종류만 스폰되는 일을 막는다.
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
                !enemy.Prefab.TryGetComponent<NetworkObject>(out _) ||
                !enemy.Prefab.TryGetComponent<EnemyController>(out _) ||
                !enemy.Prefab.TryGetComponent<EnemyAIBrain>(out _))
            {
                Debug.LogError(
                    $"Enemy prefab requires NetworkObject, EnemyController, and EnemyAIBrain: {enemy.EnemyId}");
                return;
            }

            definitions.Add(enemy);
        }

        EnemySquad squad = new EnemySquad();

        for (int entryIndex = 0; entryIndex < spawnInstruction.Enemies.Count; entryIndex++)
        {
            EnemyDefinition enemy = definitions[entryIndex];
            int count = spawnInstruction.Enemies[entryIndex].Count;

            for (int i = 0; i < count; i++)
            {
                // 같은 위치에 겹쳐 생성될 가능성을 줄이도록 반지름 안에 위치를 분산한다.
                Vector2 offset = Random.insideUnitCircle * spawnPoint.SpawnRadius;
                Vector3 spawnPosition =
                    spawnPoint.Position + new Vector3(offset.x, offset.y, 0f);

                GameObject spawnedMonster = Instantiate(
                    enemy.Prefab,
                    spawnPosition,
                    spawnPoint.Rotation);

                EnemyController controller = spawnedMonster.GetComponent<EnemyController>();
                // 네트워크 스폰 전에 분대 소속으로 지정해 개별 타깃 선택을 중지한다.
                if (!squad.AddMember(controller))
                {
                    Debug.LogError($"Failed to register enemy in squad: {enemy.EnemyId}");
                    Destroy(spawnedMonster);
                    continue;
                }

                // 생성된 적을 NGO에 등록해 클라이언트에도 나타나게 한다.
                NetworkObject networkObject = spawnedMonster.GetComponent<NetworkObject>();
                networkObject.Spawn();
            }
        }

        // 한 명도 생성되지 않았다면 빈 분대는 보관하지 않는다.
        if (!squad.TryGetCenterPosition(out _))
            return;

        // 모든 구성원이 모인 뒤 분대 중심에서 최초 공동 목표를 선택한다.
        bool hasTarget = squad.SetTarget();
        // 최초 후보가 없어도 분대를 보관해 이후 Update에서 다시 찾을 수 있게 한다.
        _squads.Add(squad);
        if (!hasTarget)
            Debug.LogWarning($"No targetable Player or Turret found for squad at {spawnInstruction.SpawnPointId}.");
    }
}

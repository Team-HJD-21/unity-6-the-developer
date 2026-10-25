// 확정된 분대 명령을 서버에서 실행하고 실제 생성/제거 사실을 발행합니다.
// 요청 context는 관측 상관관계에만 쓰며 생성 정책을 바꾸지 않습니다.
using System.Collections.Generic;
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Planner가 확정한 SpawnInstruction 하나를 서버에서 하나의 분대로 생성한다.
/// 스폰 지점과 적 프리팹을 확인하고, 생성된 적을 분대에 등록한 뒤 NGO로 동기화한다.
/// 생성 수와 프리셋 구성은 다시 결정하지 않는다.
/// </summary>
public class EnemySpawnExecutor : NetworkBehaviour, IBattlefieldEnemyPositionSource
{
    /// <summary>
    /// 생성된 적의 네트워크 객체와 분대 소속을 함께 보관한다.
    /// Despawn 시 두 목록을 같은 식별자로 정리하기 위한 서버 측 기록이다.
    /// </summary>
    private readonly struct TrackedEnemy
    {
        public readonly NetworkObject NetworkObject;
        public readonly EnemyController Controller;
        public readonly EnemyNetworkController NetworkController;
        public readonly EnemySquad Squad;
        public readonly EncounterExecutionContext Context;
        public readonly string EnemyType;
        public readonly string SpawnPointId;
        public readonly string NetworkId;
        public readonly EnemyHealth Health;

        public TrackedEnemy(
            NetworkObject networkObject,
            EnemyController controller,
            EnemyNetworkController networkController,
            EnemySquad squad, EncounterExecutionContext context, string enemyType, string spawnPointId)
        {
            NetworkObject = networkObject;
            Controller = controller;
            NetworkController = networkController;
            Squad = squad;
            Context = context;
            EnemyType = enemyType;
            SpawnPointId = spawnPointId;
            NetworkId = networkObject.NetworkObjectId.ToString();
            Health = networkObject.GetComponent<EnemyHealth>();
        }
    }

    [SerializeField] private EnemyCatalog enemyList;
    private SpawnPointRegistry _spawnPointList = new();
    private readonly List<GameObject> _spawnSceneRoots = new();
    private readonly List<SpawnPoint> _discoveredSpawnPoints = new();
    private readonly List<EnemySquad> _squads = new();
    private readonly Dictionary<ulong, TrackedEnemy> _spawnedEnemies = new();
    public event Action<EnemyLifecycleObservation> LifecycleObserved;

    public int CountForMatch(MatchId matchId)
    {
        int count = 0;
        foreach (TrackedEnemy enemy in _spawnedEnemies.Values)
            if (enemy.Context != null && enemy.Context.MatchId == matchId) count++;
        return count;
    }

    /// <summary>
    /// Planner와 Executor가 함께 사용하는 스폰 지점 및 점령 상태 목록을 반환한다.
    /// </summary>
    public SpawnPointRegistry SpawnPointList
    {
        get
        {
            // Update마다 전장을 검색하지 않고 계획 요청 직전에 추가/복제/제거를 반영합니다.
            RefreshSpawnPoints();
            return _spawnPointList;
        }
    }

    /// <summary>현재 네트워크 서버 권한으로 실제 생성을 수행할 수 있는지 반환합니다.</summary>
    public bool CanSpawn => IsSpawned && IsServer;

    /// <summary>Executor가 추적 중인 현재 네트워크 적 수를 반환합니다.</summary>
    public int SpawnedEnemyCount => _spawnedEnemies.Count;

    /// <summary>
    /// 현재 네트워크에 생성되어 있는 적의 식별자와 XY 위치만 복사해 반환한다.
    /// </summary>
    public IReadOnlyList<EnemySpatialInput> CaptureEnemyPositions()
    {
        var positions = new List<EnemySpatialInput>(_spawnedEnemies.Count);
        var staleIds = new List<ulong>();
        foreach (KeyValuePair<ulong, TrackedEnemy> enemy in _spawnedEnemies)
        {
            NetworkObject networkObject = enemy.Value.NetworkObject;
            if (networkObject == null || !networkObject.IsSpawned)
            {
                staleIds.Add(enemy.Key);
                continue;
            }

            Vector3 position = networkObject.transform.position;
            positions.Add(new EnemySpatialInput(
                new TeamHJD.Game.Domain.EntityId($"enemy-{networkObject.NetworkObjectId}"),
                new BattlefieldPoint(position.x, position.y)));
        }

        foreach (ulong staleId in staleIds)
            UntrackEnemy(staleId);
        return positions.AsReadOnly();
    }

    /// <summary>
    /// 같은 Scene의 SpawnPoint를 수집해 Planner의 계획에 사용할 목록을 초기화한다.
    /// </summary>
    void Awake()
    {
        RefreshSpawnPoints();
    }

    /// <summary>
    /// 부모 이름/Executor 계층과 관계없이 같은 Scene의 모든 지점을 등록합니다.
    /// 다른 Scene의 지점은 포함하지 않습니다.
    /// 비활성 지점도 보관하며, 실제 생성 가능 여부와 공간 배정은 기존 정책이 판단합니다.
    /// </summary>
    public void RefreshSpawnPoints()
    {
        _discoveredSpawnPoints.Clear();
        var scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            _spawnPointList.Initialize(_discoveredSpawnPoints);
            return;
        }
        _spawnSceneRoots.Clear();
        scene.GetRootGameObjects(_spawnSceneRoots);
        foreach (GameObject root in _spawnSceneRoots)
            foreach (SpawnPoint point in root.GetComponentsInChildren<SpawnPoint>(true))
                _discoveredSpawnPoints.Add(point);
        _spawnPointList.Initialize(_discoveredSpawnPoints);
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
        foreach (TrackedEnemy enemy in _spawnedEnemies.Values)
        {
            Observe(enemy, GameDiagnosticKind.EnemyRemoved, "Executor shutdown");
            if (enemy.NetworkController != null)
                enemy.NetworkController.Despawned -= OnEnemyDespawned;
            if (enemy.Health != null) enemy.Health.Died -= OnEnemyDied;
        }

        _squads.Clear();
        _spawnedEnemies.Clear();
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// 적이 NGO에서 제거되면 생성 기록과 분대 소속을 함께 정리한다.
    /// </summary>
    /// <param name="networkObjectId">Despawn된 적의 NGO 식별자.</param>
    private void OnEnemyDespawned(ulong networkObjectId)
    {
        UntrackEnemy(networkObjectId);
    }

    private void OnEnemyDied(ulong networkObjectId)
    {
        if (_spawnedEnemies.TryGetValue(networkObjectId, out TrackedEnemy enemy))
            Observe(enemy, GameDiagnosticKind.EnemyDied, "EnemyHealth confirmed death");
    }

    /// <summary>
    /// 생성 기록을 해제하고 마지막 분대원이 사라졌다면 빈 분대도 제거한다.
    /// </summary>
    /// <param name="networkObjectId">해제할 적의 NGO 식별자.</param>
    private void UntrackEnemy(ulong networkObjectId)
    {
        if (!_spawnedEnemies.TryGetValue(networkObjectId, out TrackedEnemy enemy))
            return;

        _spawnedEnemies.Remove(networkObjectId);
        Observe(enemy, GameDiagnosticKind.EnemyRemoved, "Despawn / untracked (not a kill)");
        if (enemy.NetworkController != null)
            enemy.NetworkController.Despawned -= OnEnemyDespawned;
        if (enemy.Health != null) enemy.Health.Died -= OnEnemyDied;

        enemy.Squad.RemoveMember(enemy.Controller);
        if (enemy.Squad.IsEmpty)
            _squads.Remove(enemy.Squad);
    }
    
    /// <summary>
    /// 서버에서 분대 생성 명령의 지점·수량·프리팹을 검증하고 적을 생성한다.
    /// 구성원을 같은 분대에 등록한 뒤 네트워크에 스폰하고 공동 목표를 지정한다.
    /// 계획 후 지점이 점령되어 생성할 수 없게 되었다면 명령을 실행하지 않는다.
    /// </summary>
    /// <param name="spawnInstruction">분대의 스폰 지점과 적별 수량이 담긴 명령.</param>
    public int Execute(SpawnInstruction spawnInstruction)
        => Execute(spawnInstruction, null);

    /// <summary>정책은 그대로 실행하며 관측용 요청 식별자만 생성 기록에 연결합니다.</summary>
    public int Execute(SpawnInstruction spawnInstruction, EncounterExecutionContext context)
        => Execute(spawnInstruction, context, null);

    public int Execute(SpawnInstruction spawnInstruction, EncounterExecutionContext context,
        Action<SpawnDecisionSnapshot.Actual> observeSpawn, Action<string> observeFailure=null)
    {
        // NGO 스폰은 서버에서만 수행하며, 적 구성 항목이 없는 명령은 무시한다.
        if (!IsServer || spawnInstruction.Enemies == null ||
            spawnInstruction.Enemies.Count == 0)
            return Fail("Not server / empty composition",observeFailure);

        if (enemyList == null || _spawnPointList == null)
        {
            Debug.LogError("Spawner dependencies are not assigned.");
            return Fail("Enemy catalog / registry missing",observeFailure);
        }

        // 명령 하나가 분대 하나이므로 모든 구성원이 공유할 스폰 지점을 한 번만 찾는다.
        if (!_spawnPointList.TryGetSpawnPoint(
                spawnInstruction.SpawnPointId,
                out SpawnPoint spawnPoint))
        {
            Debug.LogError($"SpawnPoint not found: {spawnInstruction.SpawnPointId}");
            return Fail("Spawn point missing at execution",observeFailure);
        }

        // 계획 이후 점령 상태가 바뀐 지점에서는 적을 생성하지 않는다.
        if (!spawnPoint.CanSpawnEnemies)
            return Fail("Spawn point disabled / captured after planning",observeFailure);

        // 생성 전에 모든 적 정의와 필수 컴포넌트를 확인해 일부 종류만 스폰되는 일을 막는다.
        List<EnemyDefinition> definitions = new(spawnInstruction.Enemies.Count);
        foreach (EnemySpawnEntry entry in spawnInstruction.Enemies)
        {
            if (string.IsNullOrWhiteSpace(entry.EnemyId) || entry.Count <= 0 ||
                !enemyList.TryGetDefinition(entry.EnemyId, out EnemyDefinition enemy))
            {
                Debug.LogError($"Invalid enemy spawn entry: {entry.EnemyId}");
                return Fail("Invalid catalog entry: "+entry.EnemyId,observeFailure);
            }

            if (enemy.Prefab == null ||
                !enemy.Prefab.TryGetComponent<NetworkObject>(out _) ||
                !enemy.Prefab.TryGetComponent<EnemyController>(out _) ||
                !enemy.Prefab.TryGetComponent<EnemyNetworkController>(out _))
            {
                Debug.LogError(
                    $"Enemy prefab requires NetworkObject, EnemyController, and EnemyNetworkController: {enemy.EnemyId}");
                return Fail("Enemy prefab missing required components: "+enemy.EnemyId,observeFailure);
            }

            definitions.Add(enemy);
        }

        EnemySquad squad = new EnemySquad();
        int spawnedCount = 0;
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
                    Fail("Failed to register squad member: "+enemy.EnemyId,observeFailure);
                    Destroy(spawnedMonster);
                    continue;
                }

                // 생성된 적을 NGO에 등록해 클라이언트에도 나타나게 한다.
                NetworkObject networkObject = spawnedMonster.GetComponent<NetworkObject>();
                EnemyNetworkController networkController =
                    spawnedMonster.GetComponent<EnemyNetworkController>();
                networkObject.Spawn();
                _spawnedEnemies[networkObject.NetworkObjectId] =
                    new TrackedEnemy(networkObject, controller, networkController, squad,
                        context, enemy.EnemyId, spawnInstruction.SpawnPointId);
                networkController.Despawned += OnEnemyDespawned;
                EnemyHealth health = spawnedMonster.GetComponent<EnemyHealth>();
                if (health != null) health.Died += OnEnemyDied;
                if (observeSpawn != null)
                    try { observeSpawn(new SpawnDecisionSnapshot.Actual(spawnInstruction.SpawnPointId,enemy.EnemyId,
                        networkObject.NetworkObjectId.ToString(),new BattlefieldPoint(networkObject.transform.position.x,networkObject.transform.position.y))); }
                    catch (Exception exception) { Debug.LogException(exception,this); }
                Observe(_spawnedEnemies[networkObject.NetworkObjectId], GameDiagnosticKind.EnemySpawned, "Network spawn succeeded");
                spawnedCount++;
            }
        }

        // 한 명도 생성되지 않았다면 빈 분대는 보관하지 않는다.
        if (!squad.TryGetCenterPosition(out _))
            return spawnedCount;

        // 모든 구성원이 모인 뒤 분대 중심에서 최초 공동 목표를 선택한다.
        bool hasTarget = squad.SetTarget();
        // 최초 후보가 없어도 분대를 보관해 이후 Update에서 다시 찾을 수 있게 한다.
        _squads.Add(squad);
        if (!hasTarget)
            Debug.LogWarning($"No targetable Player or Turret found for squad at {spawnInstruction.SpawnPointId}.");
        return spawnedCount;
    }

    private void Observe(TrackedEnemy enemy, GameDiagnosticKind kind, string reason)
    {
        var observation = new EnemyLifecycleObservation(enemy.Context, kind, enemy.EnemyType, enemy.SpawnPointId, enemy.NetworkId, reason);
        // 관측 구독 실패가 스폰이나 제거 처리를 중단하지 않도록 격리합니다.
        if (LifecycleObserved == null) return;
        foreach (Action<EnemyLifecycleObservation> observer in LifecycleObserved.GetInvocationList())
            try { observer(observation); } catch (Exception exception) { Debug.LogException(exception, this); }
    }
    private int Fail(string reason,Action<string> observer)
    {
        if(observer!=null)
            try {observer(reason);}catch(Exception exception){Debug.LogException(exception,this);}
        return 0;
    }
}

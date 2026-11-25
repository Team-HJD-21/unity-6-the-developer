using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
using UnityEngine;

/// <summary>EnemyTest 전용 조작을 Core의 공개 Match 정보와 Encounter API에 연결한다.</summary>
public static class EnemyTestSceneTools
{
    public const string ScenePath = "Assets/Scenes/Tests/EnemyTest.unity";
    private const int TestSpawnBatchSize = 100;
    private static long _individualSequence;

    /// <summary>운영 씬이나 다른 실행기를 테스트 조작 대상으로 사용하지 않는다.</summary>
    public static bool IsTestScene(EncounterSceneBinding binding)
        => binding != null && binding.ExecutionTarget != null &&
           binding.ExecutionTarget.gameObject.scene.path == ScenePath;

    private static InitialStageCompositionRoot RequireMatch(EncounterSceneBinding binding)
    {
        if (!IsTestScene(binding) || !binding.IsServer || binding.BoundMatchId.IsEmpty)
            throw new InvalidOperationException("EnemyTest 씬의 Core Host와 Match가 준비되어야 합니다.");
        var scene = binding.ExecutionTarget.gameObject.scene;
        var composition = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<InitialStageCompositionRoot>())
            .SingleOrDefault();
        if (composition == null || composition.MatchCoordinator == null ||
            composition.MatchCoordinator.MatchId != binding.BoundMatchId ||
            !composition.MatchCoordinator.IsGameplayRunning || composition.TacticalBattlefield == null)
            throw new InvalidOperationException("현재 EnemyTest의 권위 Match가 실행 중이어야 합니다.");
        return composition;
    }

    /// <summary>Core가 확정한 Host 참가자를 요청자로 사용한다.</summary>
    public static bool RequestSpawn(EncounterSceneBinding binding, int maximum, out string result)
    {
        RequireMatch(binding);
        if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        var network = binding.ExecutionTarget.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<InitialStageNetworkBinding>()).Single();
        var descriptor = network.Matches?.Descriptor;
        if (descriptor == null || descriptor.Config.MatchId != binding.BoundMatchId)
            throw new InvalidOperationException("현재 Match의 Core 네트워크 정보가 없습니다.");
        var issuer = descriptor.Participants.Single(player => player.IsHost).PlayerId;
        var request = EncounterRequest.Automatic(
            new MatchRequestContext(binding.BoundMatchId, Guid.NewGuid(), issuer),
            maximum, EncounterRequestSource.DebugWindow);
        var response = binding.Submit(request, issuer);
        result = response.Reason;
        return response.HasSpawned;
    }

    /// <summary>시험 씬의 실제 피해 API를 호출한다. 은신 및 공격자별 규칙도 그대로 적용한다.</summary>
    public static bool TryDamageTestEnemy(EncounterSceneBinding binding, EnemyController enemy, int damage,
        TeamHJD.Game.Enemy.Contracts.AttackSourceType source, out string result)
    {
        RequireMatch(binding);
        result = "현재 시험 씬의 생성된 몬스터와 양수 피해량을 지정하세요.";
        if (enemy == null || damage <= 0 || !Enum.IsDefined(typeof(TeamHJD.Game.Enemy.Contracts.AttackSourceType), source) ||
            enemy.gameObject.scene != binding.ExecutionTarget.gameObject.scene ||
            !enemy.TryGetComponent<EnemyNetworkController>(out var network) || !network.IsSpawned ||
            !network.IsServer || network.NetworkManager != binding.GetComponent<NetworkManager>() ||
            !enemy.TryGetComponent<EnemyHealth>(out var health)) return false;
        string enemyName = enemy.name;
        int before = health.CurrentHealth;
        if (!health.TryTakeDamage(damage, source))
        {
            result = "피해가 거부됐습니다. 공격자 종류·탐지 상태·피격 허용 상태를 확인하세요.";
            return false;
        }
        result = $"{enemyName} · {source} 피해 {damage} · 체력 {before} → {(health != null ? health.CurrentHealth : 0)}";
        return true;
    }

    /// <summary>EnemyTest의 서버 몬스터만 사망 효과 없이 제거한다.</summary>
    public static bool TryClearTestSceneEnemies(EncounterSceneBinding binding, out string result)
    {
        RequireMatch(binding);
        var scene = binding.ExecutionTarget.gameObject.scene;
        var enemies = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EnemyNetworkController>(true)).ToArray();
        int count = 0;
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsSpawned || !enemy.IsServer ||
                enemy.NetworkManager != binding.GetComponent<NetworkManager>()) continue;
            enemy.NetworkObject.Despawn(true);
            count++;
        }
        result = $"시험 씬의 몬스터 {count}마리를 소환물까지 포함해 정리했습니다. 반복 생성 요청은 별도로 중지하세요.";
        return true;
    }

    /// <summary>요청 수량을 선택한 지점에 나누고 기존 생성 요청을 지점별로 실행한다.</summary>
    public static int SpawnTestEnemies(EncounterSceneBinding binding, EnemyDefinition definition,
        IReadOnlyList<SpawnPoint> points, TargetableComponent initialTarget, int requestedCount,
        out EnemyController controller, out string result,
        EnemyTargetRule targetRule = EnemyTargetRule.PlayerOrTurret)
    {
        var composition = RequireMatch(binding);
        var snapshot = composition.TacticalBattlefield;
        if (snapshot.MatchId != binding.BoundMatchId)
            throw new InvalidOperationException("현재 Match의 전장 Snapshot이 아닙니다.");

        controller = null;
        result = "등록된 몬스터와 출현 지점을 선택하고 수량을 1~500마리로 지정하세요.";
        if (definition == null || points == null || points.Count == 0 || requestedCount < 1 || requestedCount > 500)
            return 0;

        var scene = binding.ExecutionTarget.gameObject.scene;
        var pointIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpawnPoint point in points)
        {
            if (point == null || point.gameObject.scene != scene || !point.CanSpawnEnemies ||
                string.IsNullOrWhiteSpace(point.SpawnPointId) || !pointIds.Add(point.SpawnPointId))
            {
                result = "현재 시험 씬에서 생성할 수 있는 서로 다른 출현 지점을 선택하세요.";
                return 0;
            }
        }

        if (initialTarget != null && definition.IsIncludedInAttackerLimit && initialTarget.MaxAttackers > 0)
        {
            int available = Math.Max(0, initialTarget.MaxAttackers - initialTarget.AssignedAttackerCount);
            if (requestedCount > available)
            {
                result = $"초기 타깃 {initialTarget.name}에 {available}마리까지 추가 배정할 수 있습니다. 요청: {requestedCount}마리.";
                return 0;
            }
        }

        var details = new StringBuilder();
        var spawnPlan = new List<(SpawnPoint Point, TargetableComponent Target)>();
        if (points.Count == 1)
            spawnPlan.Add((points[0], initialTarget));
        else
        {
            IReadOnlyList<TargetableComponent> candidates = initialTarget != null
                ? new[] { initialTarget }
                : scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TargetableComponent>())
                    .ToArray();

            foreach (SpawnPoint point in points)
            {
                var reachableTarget = binding.ExecutionTarget.FindReachableTestTarget(definition, point, candidates, targetRule);
                if (reachableTarget != null)
                    spawnPlan.Add((point, reachableTarget));
                else
                    details.AppendLine().Append($"{point.name}: 도달 가능한 타깃이 없어 제외");
            }

            if (spawnPlan.Count < 2)
            {
                result = $"타깃에 도달 가능한 출현 지점이 {spawnPlan.Count}곳입니다. 두 곳 이상 선택하세요.";
                if (initialTarget != null)
                    result += " 초기 타깃을 비우면 지점마다 도달 가능한 타깃을 찾습니다.";
                result += details.ToString();
                return 0;
            }
        }

        int perPoint = requestedCount / spawnPlan.Count;
        int remainder = requestedCount % spawnPlan.Count;
        int totalSpawned = 0;
        bool interrupted = false;
        for (int i = 0; i < spawnPlan.Count && !interrupted; i++)
        {
            int pointCount = perPoint + (i < remainder ? 1 : 0);
            if (pointCount == 0)
                continue;

            var (point, target) = spawnPlan[i];
            int pointSpawned = 0;
            string lastFailure = null;
            for (int remaining = pointCount; remaining > 0; remaining -= TestSpawnBatchSize)
            {
                int batchCount = Math.Min(remaining, TestSpawnBatchSize);
                var context = new EncounterExecutionContext(binding.BoundMatchId, checked(++_individualSequence),
                    snapshot.Revision, EncounterRequestSource.DebugWindow, "IndividualTest",
                    environment: composition.EnvironmentSource?.Snapshot, requestGuid: Guid.NewGuid(),
                    spawnCancellation: composition.MatchCoordinator.GameplayCancellation);
                try
                {
                    int spawned = binding.ExecutionTarget.SpawnTestEnemies(definition, point, target, context, batchCount,
                        out EnemyController first, out string message, targetRule);
                    if (controller == null)
                        controller = first;
                    pointSpawned += spawned;
                    if (spawned < batchCount)
                        lastFailure = message;
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    lastFailure = $"생성 중단: {error.Message}";
                    interrupted = true;
                    break;
                }
            }

            totalSpawned += pointSpawned;
            string targetName = target != null ? target.name : "분대 자동 선택";
            details.AppendLine().Append($"{point.name}: {pointSpawned}/{pointCount}마리 · 타깃 {targetName}");
            if (lastFailure != null)
                details.AppendLine().Append(lastFailure);
        }

        result = $"{definition.EnemyId} {totalSpawned}/{requestedCount}마리 생성{details}";
        return totalSpawned;
    }
}

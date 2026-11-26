#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.AI;

public partial class EnemySpawnExecutor
{
    /// <summary>시험 출현 지점에서 완전한 경로가 있는 타깃을 고른다.</summary>
    public TargetableComponent FindReachableTestTarget(EnemyDefinition definition, SpawnPoint point,
        IReadOnlyList<TargetableComponent> candidates, EnemyTargetRule targetRule)
    {
        if (definition.Prefab == null)
            return null;

        var movement = definition.Prefab.GetComponent<EnemyMovement>();
        var filter = movement != null
            ? movement.GetNavigationFilter(gameObject.scene)
            : new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        Vector3 origin = point.Position;
        origin.z = 0f;
        if (!NavMesh.SamplePosition(origin, out NavMeshHit start, point.SpawnRadius + 0.001f, filter))
            return null;

        float projectionDistance = EnemyMovement.GetTargetProjectionDistance(filter.agentTypeID);

        var path = new NavMeshPath();
        TargetableComponent best = null;
        bool bestIsTestTarget = false;
        float bestDistance = float.PositiveInfinity;
        foreach (TargetableComponent target in candidates)
        {
            if (target == null || !target.CanBeTargeted || target.gameObject.scene != gameObject.scene ||
                (targetRule == EnemyTargetRule.PlayerOnly && target.TargetType != TargetType.Player) ||
                (targetRule == EnemyTargetRule.TurretOnly && target.TargetType != TargetType.Turret) ||
                (definition.IsIncludedInAttackerLimit && target.MaxAttackers > 0 &&
                 target.AssignedAttackerCount >= target.MaxAttackers))
                continue;

            bool isTestTarget = target.TryGetComponent<EnemyDamageTestTarget>(out _);
            if (bestIsTestTarget && !isTestTarget)
                continue;

            Vector3 goal = target.TargetTransform.position;
            goal.z = 0f;
            float distance = (start.position - goal).sqrMagnitude;
            if (isTestTarget == bestIsTestTarget && distance >= bestDistance)
                continue;
            if (!NavMesh.SamplePosition(goal, out NavMeshHit destination, projectionDistance, filter) ||
                !NavMesh.CalculatePath(start.position, destination.position, filter, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                continue;

            best = target;
            bestIsTestTarget = isTestTarget;
            bestDistance = distance;
        }
        return best;
    }

    /// <summary>Editor 시험에서 한 종류를 출현 지점에 생성하고 실제 생성한 수를 반환한다.</summary>
    public int SpawnTestEnemies(EnemyDefinition definition, SpawnPoint point, TargetableComponent initialTarget,
        EncounterExecutionContext context, int requestedCount, out EnemyController controller, out string result,
        EnemyTargetRule targetRule = EnemyTargetRule.PlayerOrTurret)
    {
        controller = null;
        if (requestedCount < 1 || requestedCount > 100)
        {
            result = "시험 생성 수는 1~100마리여야 합니다.";
            return 0;
        }

        result = "Host·현재 Match·같은 씬의 유효한 출현 지점과 카탈로그 정의가 필요합니다.";
        if (!Enum.IsDefined(typeof(EnemyTargetRule), targetRule) ||
            !CanSpawn || !CanSimulateField || context == null || context.MatchId.IsEmpty ||
            context.SpawnCancellation.IsCancellationRequested || point == null ||
            point.gameObject.scene != gameObject.scene || !point.CanSpawnEnemies || definition == null ||
            enemyList == null || !enemyList.TryGetDefinition(definition.EnemyId, out var registered) || registered != definition)
            return 0;

        if (initialTarget != null && (!initialTarget.CanBeTargeted ||
            initialTarget.gameObject.scene != gameObject.scene ||
            (targetRule == EnemyTargetRule.PlayerOnly && initialTarget.TargetType != TargetType.Player) ||
            (targetRule == EnemyTargetRule.TurretOnly && initialTarget.TargetType != TargetType.Turret)))
        {
            result = "초기 타깃의 씬·타깃 가능 상태·유형을 확인하세요. PlayerOnly는 플레이어만, TurretOnly는 시설만 지정할 수 있습니다.";
            return 0;
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

        RefreshSpawnPoints();
        var instruction = new SpawnInstruction("IndividualTest", point.SpawnPointId,
            new[] { new EnemySpawnEntry(definition.EnemyId, requestedCount) }, targetRule: targetRule);
        string failure = null;
        List<TrackedEnemy> spawned;
        try
        {
            Execute(instruction, context, null, reason => failure = reason, preferredTarget: initialTarget);
            spawned = GetTestSpawns(context);

            if (initialTarget != null)
            {
                // 타깃 규칙에 따라 자동 선택된 목표가 다르면 사용자가 지정한 초기 타깃을 연결한다.
                var assignedSquads = new HashSet<EnemySquad>();
                foreach (var tracked in spawned)
                {
                    if (!assignedSquads.Add(tracked.Squad) || tracked.Squad.CheckpointTarget == initialTarget.transform)
                        continue;

                    tracked.Squad.RestoreCheckpointTarget(initialTarget.transform);
                    if (tracked.Squad.CheckpointTarget != initialTarget.transform)
                    {
                        DespawnTestSpawns(spawned);
                        result = "지정한 초기 타깃 배정에 실패해 이번 요청으로 생성한 몬스터를 정리했습니다.";
                        return 0;
                    }
                }
            }
        }
        catch
        {
            DespawnTestSpawns(GetTestSpawns(context));
            throw;
        }

        int count = spawned.Count;
        if (count > 0)
            controller = spawned[0].Controller;

        result = $"{definition.EnemyId} {count}/{requestedCount}마리 생성 · 지점 {point.name} · 초기 타깃 {(initialTarget != null ? initialTarget.name : "분대 자동 선택")}";
        if (count < requestedCount && !string.IsNullOrEmpty(failure))
            result += $"\n생성 실패 사유: {failure}";
        return count;
    }

    private List<TrackedEnemy> GetTestSpawns(EncounterExecutionContext context)
    {
        var spawned = new List<TrackedEnemy>();
        foreach (var tracked in _spawnedEnemies.Values)
            if (ReferenceEquals(tracked.Context, context) && tracked.NetworkObject.IsSpawned)
                spawned.Add(tracked);
        return spawned;
    }

    private static void DespawnTestSpawns(List<TrackedEnemy> spawned)
    {
        foreach (var tracked in spawned)
            if (tracked.NetworkObject != null && tracked.NetworkObject.IsSpawned)
                tracked.NetworkObject.Despawn(true);
    }
}
#endif

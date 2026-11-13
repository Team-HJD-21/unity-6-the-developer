// Enemy가 확정한 소환 종류·수량·초기 타깃을 공통 생성 경계에 전달합니다.
// 정규 생산 예산을 사용하지 않으며, 실제 생성된 소환도 통계·위치·저장·Match 종료에 포함합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.AI;

internal interface IEnemySummonRequests
{
    EncounterExecutionContext BeginSummon(EncounterExecutionContext parent, string enemyId, int count);
    void CompleteSummon(EncounterExecutionContext request, int count, int actual, bool countsKnown, string reason);
}

public partial class EnemySpawnExecutor
{
    internal IEnemySummonRequests SummonRequests { get; set; }

    /// <summary>EnemySummonGenerator가 호출합니다. 부모의 소속을 확인하고 지정된 위치 근처의 NavMesh에 소환합니다.</summary>
    public int SpawnSummoned(EnemyNetworkController parent, EnemyDefinition definition,
        IReadOnlyList<Vector3> positions, Transform initialTarget)
    {
        if (!CanSpawn || !CanSimulateField || parent == null || !parent.IsSpawned ||
            parent.NetworkManager != NetworkManager || !_spawnedEnemies.TryGetValue(parent.NetworkObjectId, out var source) ||
            source.Context == null || source.Context.SpawnCancellation.IsCancellationRequested ||
            definition == null || positions == null || positions.Count == 0 || SummonRequests == null)
            return 0;
        // 사망 반응의 소환은 Health가 이미 0인 상태에서 호출됩니다. Despawn 전의 추적 소속으로 검증합니다.
        // 저장 복원에 쓰는 원본과 같은 Definition이어야 합니다. ID만 같은 임의 프리팹은 허용하지 않습니다.
        if (enemyList == null || !enemyList.TryGetDefinition(definition.EnemyId, out var registered) ||
            registered != definition || definition.Prefab == null ||
            !definition.Prefab.TryGetComponent<Unity.Netcode.NetworkObject>(out _) ||
            !definition.Prefab.TryGetComponent<EnemyController>(out _) ||
            !definition.Prefab.TryGetComponent<EnemyNetworkController>(out _))
            return 0;

        var requests = SummonRequests;
        var context = requests.BeginSummon(source.Context, definition.EnemyId, positions.Count);
        if (context == null) return 0;
        var squads = new List<EnemySquad>();
        EnemySquad shared = definition.SquadMode == EnemySquadMode.Shared
            ? new EnemySquad(definition.SquadMode, definition.TargetRule, initialTarget) : null;
        if (shared != null) squads.Add(shared);
        int actual = 0;
        bool countsKnown = true;
        string reason = "";
        try
        {
            foreach (var position in positions)
            {
                if (context.SpawnCancellation.IsCancellationRequested || !CanSimulateField) break;
                if (!TryResolveSummonPosition(position, out var resolved))
                {
                    reason = "Requested summon location has no nearby NavMesh surface.";
                    continue;
                }
                var squad = shared ?? new EnemySquad(definition.SquadMode, definition.TargetRule, initialTarget);
                if (shared == null) squads.Add(squad);
                if (TrySpawnActor(definition, resolved, Quaternion.identity, squad, context,
                        "summon/" + source.EntityId, observeFailure: failure => reason = failure))
                    actual++;
            }
            return actual;
        }
        catch (Exception error)
        {
            countsKnown = false;
            reason = error.GetType().Name + ": " + error.Message;
            throw;
        }
        finally
        {
            foreach (var squad in squads) if (!squad.IsEmpty) _squads.Add(squad);
            requests.CompleteSummon(context, positions.Count, actual, countsKnown, reason);
        }
    }

    internal static bool TryResolveSummonPosition(Vector3 requested, out Vector3 resolved)
    {
        resolved = default;
        if (!float.IsFinite(requested.x) || !float.IsFinite(requested.y) || !float.IsFinite(requested.z) ||
            !NavMesh.SamplePosition(new Vector3(requested.x, requested.y, 0), out var hit, .5f, NavMesh.AllAreas))
            return false;
        resolved = new Vector3(hit.position.x, hit.position.y, requested.z);
        return true;
    }
}

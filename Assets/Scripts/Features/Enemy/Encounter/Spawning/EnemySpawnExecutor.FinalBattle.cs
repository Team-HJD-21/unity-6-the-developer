// 최종 Scene의 지정된 보스 한 개를 정규 Encounter와 같은 실제 생성 경계로 전달합니다.
// 전환의 공용 정지 중에도 준비만 허용하며, 생성된 AI/공격은 공용 시각이 재개될 때 진행합니다.
using System;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using EntityId = TeamHJD.Game.Domain.EntityId;

public partial class EnemySpawnExecutor
{
    internal bool TrySpawnFinalBoss(EnemyDefinition definition, Transform spawn, MatchId match, CancellationToken gameplay,
        out EntityId identity, out string reason)
    {
        identity = default; reason = "FinalBossDefinitionOrExecutorUnavailable";
        if (!CanSpawn || _fieldTime == null || !_fieldTime.IsAvailable || _fieldTime.Snapshot.MatchId != match ||
            _gameplay.IsCancellationRequested || gameplay.IsCancellationRequested || definition == null ||
            spawn == null || spawn.gameObject.scene != gameObject.scene || enemyList == null ||
            !enemyList.TryGetDefinition(definition.EnemyId, out var registered) || registered != definition ||
            definition.Prefab == null || !definition.Prefab.TryGetComponent<NetworkObject>(out _) ||
            !definition.Prefab.TryGetComponent<EnemyController>(out _) || !definition.Prefab.TryGetComponent<EnemyHealth>(out _) ||
            !definition.Prefab.TryGetComponent<EnemyNetworkController>(out _)) return false;
        if (!NavMesh.SamplePosition(new Vector3(spawn.position.x, spawn.position.y, 0), out var hit, .5f, NavMesh.AllAreas))
        { reason = "FinalBossRequiresAuthoredNavMeshSpawn"; return false; }
        if (SpawnedEnemyCount != 0) { reason = "FinalBossRequiresDedicatedEmptyExecutor"; return false; }

        var squad = new EnemySquad(definition.SquadMode, definition.TargetRule);
        var expected = new EntityId("final/" + match.Value);
        var context = new EncounterExecutionContext(match, 1, 0, EncounterRequestSource.StageProgression,
            "FinalBattle", requestGuid: Guid.NewGuid(), spawnCancellation: gameplay);
        string failure = null;
        // 정지 우회는 보스 준비 한 경로에서만 사용합니다. 취소된 Match/요청은 공통 생성기에서 계속 거부합니다.
        if (!TrySpawnActor(definition, new Vector3(hit.position.x, hit.position.y, spawn.position.z), spawn.rotation,
                squad, context, spawn.name, observeFailure: error => failure = error, identity: expected, prepareWhilePaused: true))
        { reason = failure ?? "FinalBossCreationFailed"; return false; }
        _squads.Add(squad); squad.SetTarget();
        identity = expected; reason = null;
        return true;
    }
}

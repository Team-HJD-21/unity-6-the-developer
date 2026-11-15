// 최종 Scene의 지정된 보스 한 개를 정규 Encounter와 같은 실제 생성 경계로 전달합니다.
// 전환의 공용 정지 중에도 준비만 허용하며, 생성된 AI/공격은 공용 시각이 재개될 때 진행합니다.
using System;
using System.Threading;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using EntityId = TeamHJD.Game.Domain.EntityId;

public partial class EnemySpawnExecutor
{
    internal void AbandonFinalBossAttempt(MatchId match)
    {
        RequireCheckpointMatch(match);
        if (CanSimulateField) throw new InvalidOperationException("보스전 재도전은 정지된 상태에서만 준비합니다.");
        // 재도전은 이전 공격의 중간 프레임을 이어가지 않습니다. 전용 Executor의 소환·투사체까지 버린 뒤 시작 저장을 적용합니다.
        foreach (var projectile in CheckpointProjectiles().ToArray())
            if (projectile.NetworkObject.IsSpawned) projectile.NetworkObject.Despawn(true);
        DespawnForMatch(match);
        _squads.Clear();
    }

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

        // 독립 BossRoom에는 시설이 없습니다. Additive로 보류한 Field의 터렛을 추적하지 않습니다.
        // 소환 분대도 이 원본 규칙을 상속하므로 보스·소환체가 같은 참가자를 상대합니다.
        var squad = new EnemySquad(definition.SquadMode, EnemyTargetRule.PlayerOnly);
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

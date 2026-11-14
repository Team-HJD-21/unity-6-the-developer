// 지정된 Field 사건을 일반 Enemy와 같은 생성·NGO·분대·저장 경계로 실행합니다.
// 사건의 고정 EntityId로 현재 실제 개체를 조회하며 별도 완료 목록이나 정규 생산 예산은 만들지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.AI;
using EntityId = TeamHJD.Game.Domain.EntityId;

public partial class EnemySpawnExecutor
{
    /// <summary>Stage 연결부가 재시도 전에 조회합니다. 복원된 실제 개체도 같은 ID로 찾으며 오래된 응답을 캐시하지 않습니다.</summary>
    internal bool TryFindStageActor(Guid eventId, out EntityId identity)
    {
        identity = default;
        if (_fieldTime == null || eventId == Guid.Empty) return false;
        var expected = new EntityId("stage/" + eventId.ToString("N"));
        foreach (var enemy in _spawnedEnemies.Values)
            if (enemy.EntityId == expected.Value && enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned &&
                enemy.Context?.MatchId == _fieldTime.Snapshot.MatchId)
            {
                identity = expected;
                return true;
            }
        return false;
    }

    /// <summary>Host의 지정 편성 사건만 실행합니다. 등장 전 저장과 사건 중복 판단은 호출하는 Stage 연결부가 담당합니다.</summary>
    internal bool TrySpawnStageActor(Guid eventId, EnemyDefinition definition, IReadOnlyList<Transform> entrances,
        RegionGridSnapshot regions, EncounterExecutionContext context, out EntityId identity, out string reason,
        Action<SpawnDecisionSnapshot.Actual> observeSpawn)
    {
        identity = default;
        reason = "FieldBossSpawnUnavailable";
        if (eventId == Guid.Empty || regions == null || context == null || _fieldTime == null ||
            regions.MatchId != _fieldTime.Snapshot.MatchId || context.MatchId != regions.MatchId ||
            !CanSpawn || !CanSimulateField || _gameplay.IsCancellationRequested || context.SpawnCancellation.IsCancellationRequested)
            return false;
        if (TryFindStageActor(eventId, out identity)) { reason = null; return true; }
        identity = default;
        if (definition == null || enemyList == null || !enemyList.TryGetDefinition(definition.EnemyId, out var registered) ||
            registered != definition || definition.Prefab == null ||
            !definition.Prefab.TryGetComponent<Unity.Netcode.NetworkObject>(out _) ||
            !definition.Prefab.TryGetComponent<EnemyController>(out _) ||
            !definition.Prefab.TryGetComponent<EnemyHealth>(out _) ||
            !definition.Prefab.TryGetComponent<EnemyNetworkController>(out _))
        {
            reason = "FieldBossDefinitionNotInCatalog";
            return false;
        }
        if (entrances == null) { reason = "FieldBossEntrancesNotAssigned"; return false; }
        foreach (var entrance in entrances)
        {
            if (entrance == null || !entrance.gameObject.activeInHierarchy || entrance.gameObject.scene != gameObject.scene ||
                !TryResolveStageEntrance(entrance.position, regions, out var position)) continue;
            // 서로 다른 사건을 같은 실제 위치에 겹쳐 만들지 않습니다. 제작자는 복수의 안전한 진입점을 제공합니다.
            bool occupied = false;
            foreach (var enemy in _spawnedEnemies.Values)
                if (enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned &&
                    Vector2.Distance(enemy.NetworkObject.transform.position, position) < 1f) { occupied = true; break; }
            if (occupied) continue;
            var squad = new EnemySquad(definition.SquadMode, definition.TargetRule);
            var requested = new EntityId("stage/" + eventId.ToString("N"));
            string failure = null;
            if (!TrySpawnActor(definition, position, entrance.rotation, squad, context, entrance.name,
                    observeSpawn, error => failure = error, requested))
            {
                reason = failure ?? "FieldBossCreationFailed";
                return false;
            }
            _squads.Add(squad);
            squad.SetTarget();
            identity = requested;
            reason = null;
            return true;
        }
        reason = "FieldBossRequiresFreeReservedNavMeshEntrance";
        return false;
    }

    /// <summary>Level의 지정 진입점은 Grid 안의 점유 불가 셀·NavMesh 위에 있어야 합니다. 전체 확보 뒤에도 출현 가능해야 합니다.</summary>
    internal static bool TryResolveStageEntrance(Vector3 requested, RegionGridSnapshot regions, out Vector3 position)
    {
        position = default;
        if (regions == null || !float.IsFinite(requested.x) || !float.IsFinite(requested.y) || !float.IsFinite(requested.z) ||
            !NavMesh.SamplePosition(new Vector3(requested.x, requested.y, 0), out var hit, .5f, NavMesh.AllAreas) ||
            !regions.Definition.TryGetCellIndex(new BattlefieldPoint(hit.position.x, hit.position.y), out int cell) ||
            regions.Definition.CapturableCells[cell] || regions.CapturedCells[cell]) return false;
        position = new Vector3(hit.position.x, hit.position.y, requested.z);
        return true;
    }
}

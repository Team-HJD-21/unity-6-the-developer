#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

public sealed partial class EnemyNetworkTestLauncher
{
    /// <summary>같은 시험 씬·서버의 네트워크 몬스터를 소환물까지 포함해 제거한다. 사망 효과는 실행하지 않는다.</summary>
    public bool TryClearTestSceneEnemies(out string result)
    {
        if (!IsServer || _boundMatchId.IsEmpty || ExecutionTarget == null)
        {
            result = "Host와 현재 시험 씬의 실행기가 필요합니다.";
            return false;
        }
        CancelPendingInitialEncounter();
        CancelScheduledEncounters();
        var scene = ExecutionTarget.gameObject.scene;
        var enemies = new List<EnemyNetworkController>();
        foreach (var root in scene.GetRootGameObjects())
            enemies.AddRange(root.GetComponentsInChildren<EnemyNetworkController>(true));
        int count = 0;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.gameObject.scene != scene || !enemy.IsSpawned ||
                !enemy.IsServer || enemy.NetworkManager != _networkManager)
                continue;
            enemy.NetworkObject.Despawn(true);
            count++;
        }
        result = $"시험 씬의 네트워크 몬스터 {count}마리를 소환물까지 포함해 정리했습니다.";
        return true;
    }

    /// <summary>선택한 몬스터의 실제 피해 API를 호출한다. 피격 규칙이나 사망·소환 처리를 우회하지 않는다.</summary>
    public bool TryDamageTestEnemy(EnemyController enemy, int damage, AttackSourceType source, out string result)
    {
        result = "현재 시험 씬·Host의 생성된 몬스터와 양수 피해량을 지정하세요.";
        if (!IsServer || _boundMatchId.IsEmpty || ExecutionTarget == null || enemy == null || damage <= 0 ||
            !Enum.IsDefined(typeof(AttackSourceType), source) || enemy.gameObject.scene != ExecutionTarget.gameObject.scene ||
            !enemy.TryGetComponent<EnemyNetworkController>(out var network) || !network.IsSpawned ||
            !network.IsServer || network.NetworkManager != _networkManager || !enemy.TryGetComponent<EnemyHealth>(out var health))
            return false;

        string enemyName = enemy.name;
        int before = health.CurrentHealth;
        if (!health.TryTakeDamage(damage, source))
        {
            result = "피해가 거부됐습니다. 공격자 종류·탐지 상태·피격 허용 상태·필드 정지를 확인하세요.";
            return false;
        }
        int after = health != null ? health.CurrentHealth : 0;
        result = $"{enemyName} · {source} 피해 {damage} · 체력 {before} → {after}";
        return true;
    }

    /// <summary>개별 능력 시험을 현재 Match의 생성 실행기에 전달한다. 예산과 프리셋 선택은 수행하지 않는다.</summary>
    public bool TrySpawnTestEnemy(EnemyDefinition definition, SpawnPoint point, TargetableComponent initialTarget,
        out EnemyController controller, out string result)
    {
        controller = null;
        if (!CanRequestEncounter(out result) || _boundMatchId.IsEmpty || _snapshotProvider == null || ExecutionTarget == null)
        {
            result = "현재 Match에 연결된 Host 실행기가 필요합니다. " + result;
            return false;
        }
        TeamHJD.Game.Domain.BattlefieldSpatialSnapshot snapshot;
        try { snapshot = _snapshotProvider(); }
        catch (ObjectDisposedException)
        {
            result = "종료된 Match에는 시험 몬스터를 생성할 수 없습니다.";
            return false;
        }
        if (snapshot == null || snapshot.MatchId != _boundMatchId)
        {
            result = "현재 Match의 전장 Snapshot이 아닙니다.";
            return false;
        }
        var context = new EncounterExecutionContext(_boundMatchId, checked(++_requestSequence), snapshot.Revision,
            EncounterRequestSource.DebugWindow, "IndividualTest", environment: _environmentProvider?.Invoke(),
            requestGuid: Guid.NewGuid(), spawnCancellation: _spawnGameplay);
        Emit(GameDiagnosticKind.RequestStarted, context, message: "Individual ability test: " + definition?.EnemyId);
        bool success = ExecutionTarget.TrySpawnTestEnemy(definition, point, initialTarget, context, out controller, out result);
        Emit(success ? GameDiagnosticKind.RequestCompleted : GameDiagnosticKind.RequestFailed, context,
            planned: 1, actual: success ? 1 : 0, message: result);
        return success;
    }
}
#endif

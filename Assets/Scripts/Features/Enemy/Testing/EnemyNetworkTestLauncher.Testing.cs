#if UNITY_EDITOR
using System;
using TeamHJD.Game.Contracts;
using UnityEngine;

public sealed partial class EnemyNetworkTestLauncher
{
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

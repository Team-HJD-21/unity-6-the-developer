// Enemy 스킬 소환에 독립 요청 번호와 현재 Match 관측 문맥을 부여합니다.
// 스킬의 편성을 다시 뽑거나 정규 생산 예산을 소비하지 않습니다.
using System;
using TeamHJD.Game.Contracts;

public sealed partial class EncounterSceneBinding : IEnemySummonRequests
{
    EncounterExecutionContext IEnemySummonRequests.BeginSummon(EncounterExecutionContext parent, string enemyId, int count)
    {
        if (_requests == null || _networkManager == null || !_networkManager.IsServer || !FieldCanAdvance ||
            _spawnGameplay.IsCancellationRequested || parent.MatchId != _boundMatchId)
            return null;
        var snapshot = _snapshotProvider?.Invoke();
        if (snapshot == null || snapshot.MatchId != _boundMatchId) return null;
        var environment = _environmentProvider?.Invoke();
        var context = new EncounterExecutionContext(_boundMatchId, checked(++_requestSequence), snapshot.Revision,
            EncounterRequestSource.EnemySkill, "Skill/" + enemyId, environment, Guid.NewGuid(),
            parent.RequestGuid, matchSeed: _spawnSeed, spawnCancellation: _spawnGameplay);
        Emit(GameDiagnosticKind.RequestStarted, context, message: $"Enemy skill: {enemyId} x {count}; prescribed composition.");
        return context;
    }

    void IEnemySummonRequests.CompleteSummon(EncounterExecutionContext context, int planned, int actual, bool countsKnown, string reason)
    {
        if (context.MatchId != _boundMatchId || _snapshotProvider == null) return;
        Emit(countsKnown && actual > 0 ? GameDiagnosticKind.RequestCompleted : GameDiagnosticKind.RequestFailed,
            context, planned, actual, message: string.IsNullOrEmpty(reason) ? "Enemy skill summon completed." : reason,
            countsUnknown: !countsKnown);
    }
}

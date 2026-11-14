// Stage의 33/66 사건을 지정된 실제 Enemy 생성기로 전달하고 기존 Debugger에 요청·실행 근거를 기록합니다.
// 편성·예산을 다시 추첨하지 않으며, 콘텐츠가 없으면 오류 이유를 돌려주고 일반 적으로 대체하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

public sealed partial class EncounterSceneBinding : IFieldBossSpawnService
{
    private EnemyDefinition _fieldBoss33, _fieldBoss66;
    private Transform[] _fieldBossEntrances = Array.Empty<Transform>();

    /// <summary>Scene configuration이 Match 연결 전에 호출합니다. 실제 Definition 두 개와 Level의 안전한 진입점을 전달합니다.</summary>
    public void ConfigureFieldBosses(EnemyDefinition first, EnemyDefinition second, Transform[] entrances)
    {
        if (_snapshotProvider != null) throw new InvalidOperationException("Configure Field events before binding a Match.");
        _fieldBoss33 = first;
        _fieldBoss66 = second;
        _fieldBossEntrances = entrances == null ? Array.Empty<Transform>() : (Transform[])entrances.Clone();
    }

    /// <summary>StageCheckpointGate가 실제 저장 완료 뒤 호출합니다. 현재 Host·Grid와 지정 콘텐츠를 확인하고 실제 생성 ID만 반환합니다.</summary>
    public bool TrySpawnFieldBoss(Guid eventId, int thresholdPercent, RegionGridSnapshot regions, out EntityId actor, out string reason)
    {
        actor = default;
        reason = "FieldBossMatchUnavailable";
        if (eventId == Guid.Empty || regions == null || regions.MatchId != _boundMatchId ||
            _networkManager == null || !_networkManager.IsServer || !FieldCanAdvance ||
            _spawnGameplay.IsCancellationRequested || _sceneExecutor == null || !_sceneExecutor.CanSpawn ||
            !ReferenceEquals(regions, regionBinding?.Source?.Snapshot)) return false;
        if (thresholdPercent != 33 && thresholdPercent != 66) throw new ArgumentOutOfRangeException(nameof(thresholdPercent));
        var definition = thresholdPercent == 33 ? _fieldBoss33 : _fieldBoss66;
        if (definition == null) { reason = "FieldBossDefinitionNotAssigned/" + thresholdPercent; return false; }
        // 정규 첫 생산보다 먼저 도달해도 생명/통계 구독과 소환 연결이 빠지지 않아야 합니다.
        EnsurePipeline();
        if (_pipeline?.Executor != _sceneExecutor) { reason = "FieldBossExecutorNotBound"; return false; }
        if (_sceneExecutor.TryFindStageActor(eventId, out actor)) { reason = null; return true; }
        actor = default;
        var snapshot = _snapshotProvider?.Invoke();
        if (snapshot == null || snapshot.MatchId != _boundMatchId) return false;
        var context = new EncounterExecutionContext(_boundMatchId, checked(++_requestSequence), snapshot.Revision,
            EncounterRequestSource.StageProgression, "Field/" + thresholdPercent, _environmentProvider?.Invoke(),
            Guid.NewGuid(), eventId, matchSeed: _spawnSeed, spawnCancellation: _spawnGameplay);
        var actual = new List<SpawnDecisionSnapshot.Actual>();
        Emit(GameDiagnosticKind.RequestStarted, context, message: "지정된 Field 사건 · " + thresholdPercent + "%");
        bool spawned = false, countsUnknown = false;
        try
        {
            spawned = _sceneExecutor.TrySpawnStageActor(eventId, definition, _fieldBossEntrances, regions,
                context, out actor, out reason, actual.Add);
            return spawned;
        }
        catch (Exception error)
        {
            countsUnknown = true;
            reason = error.GetType().Name + ": " + error.Message;
            return false;
        }
        finally
        {
            var decision = new SpawnDecisionSnapshot(snapshot, Array.Empty<SpawnDecisionSnapshot.Candidate>(),
                new[] { new SpawnDecisionSnapshot.Plan("Field/" + thresholdPercent, definition.EnemyId, 1, "") }, actual,
                spawned ? "Spawned" : "Rejected", "지정된 Stage 사건 · 정규 예산/편성 재추첨 없음", context.Environment);
            Emit(spawned ? GameDiagnosticKind.RequestCompleted : GameDiagnosticKind.RequestFailed, context,
                1, spawned ? 1 : 0, message: reason, spawnDecision: decision, countsUnknown: countsUnknown);
        }
    }
}

// Planner가 같은 예산에서 준비한 편성을 예고 후 공통 요청 서비스로 다시 제출합니다.
// 편성/비용을 재선정하지 않고 권한·현재 배치·생성·통계·등록은 기존 실행 경계에서 확인합니다.
// 원격 예고 전달이 없는 현재 연결에서는 1인 Host에만 내부 추가 출현을 허용합니다.
using System;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Enemies.Encounter;
using UnityEngine;

public sealed partial class EncounterSceneBinding : IEncounterDeferredSpawnSink
{
    private Sprite _warningSprite;
    private bool CanShowInternalWarning => _warningSprite != null && _spawnClock != null &&
        _networkManager != null && _networkManager.IsHost && _networkManager.ConnectedClientsList.Count == 1 &&
        _warningSprite.bounds.size.x > 0 && _warningSprite.bounds.size.y > 0 && regionBinding?.Source != null &&
        !_spawnGameplay.IsCancellationRequested;
    bool IEncounterDeferredSpawnSink.CanShowWarning => CanShowInternalWarning;
    bool IEncounterDeferredSpawnSink.TrySchedulePrepared(EncounterDeferredSpawn plan, EncounterExecutionContext cause)
    {
        if (!CanShowInternalWarning || cause.MatchId != _boundMatchId || cause.SpawnCancellation.IsCancellationRequested) return false;
        var instruction = plan.Instruction;
        var request = new EncounterRequest(new MatchRequestContext(_boundMatchId, Guid.NewGuid(), _authorityIssuer),
            new DefinitionId(instruction.SquadOrder), instruction.Enemies.Sum(entry => entry.Count),
            EncounterRequestSource.SituationEvent, instruction.Placement.Origin, cause.RequestGuid);
        bool queued = TryScheduleEncounterCore(request, plan.WarningSeconds, out _, plan);
        if (queued)
            GameAlerts.Show($"{plan.WarningSeconds:F0}초 뒤 점유 지역 내부에 적이 출현합니다. 주황색 표시를 확인하세요.",
                AlertTone.Info, "encounter/additional-warning");
        // Alert 수락 여부로 게임 결과를 바꾸지 않습니다. 실제 월드 예고는 예약 전체 수명 동안 유지됩니다.
        return queued;
    }

    private static void ReleaseWarning(PendingSpawn pending)
    {
        if (pending.Warning != null)
        {
            pending.Warning.enabled = false;
            UnityEngine.Object.Destroy(pending.Warning.gameObject);
        }
        pending.Warning = null;
    }

    // 예고 단계에서도 어떤 편성을 어디에 둘지 기록합니다. 아직 실제 생성/생존으로 집계하지 않습니다.
    private SpawnDecisionSnapshot CapturePreparedDecision(PendingSpawn pending)
    {
        if (pending.Prepared == null) return null;
        BattlefieldSpatialSnapshot snapshot;
        try { snapshot = _snapshotProvider?.Invoke(); }
        catch (ObjectDisposedException) { return null; }
        if (snapshot == null || snapshot.MatchId != _boundMatchId) return null;
        var instruction = pending.Prepared.Instruction;
        var plans = instruction.Enemies.Select(entry =>
            new SpawnDecisionSnapshot.Plan("", entry.EnemyId, entry.Count, "")).ToArray();
        return new SpawnDecisionSnapshot(snapshot, Array.Empty<SpawnDecisionSnapshot.Candidate>(), plans,
            Array.Empty<SpawnDecisionSnapshot.Actual>(), "Scheduled", pending.Prepared.Reason,
            _environmentProvider?.Invoke(), "prepared", instruction.Placement);
    }
}

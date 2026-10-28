// 한 요청의 확정 결과와 진단용 실행 번호를 반환합니다.
// 부분 생성·실패도 보관하므로 같은 요청으로 전체 생성을 재시도하지 않습니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public sealed class EncounterRequestResult
    {
        public Guid RequestId { get; }
        public long ExecutionSequence { get; }
        public long SnapshotRevision { get; }
        public long EnvironmentRevision { get; }
        public EncounterRequestStatus Status { get; }
        public int PlannedCount { get; }
        public int SpawnedCount { get; }
        public string Reason { get; }
        public bool IsReplay { get; }
        public bool HasSpawned => SpawnedCount > 0;
        public bool HasKnownExecutionCounts => Status != EncounterRequestStatus.ExecutionFailed;
        public EncounterRequestResult(Guid requestId, long executionSequence, long snapshotRevision,
            EncounterRequestStatus status, int plannedCount = 0, int spawnedCount = 0, string reason = "", bool isReplay = false,
            long environmentRevision = 0)
        {
            if (plannedCount < 0 || spawnedCount < 0 || spawnedCount > plannedCount) throw new ArgumentOutOfRangeException(nameof(spawnedCount));
            if (environmentRevision < 0) throw new ArgumentOutOfRangeException(nameof(environmentRevision));
            EnvironmentRevision = environmentRevision;
            RequestId = requestId; ExecutionSequence = executionSequence; SnapshotRevision = snapshotRevision;
            Status = status; PlannedCount = plannedCount; SpawnedCount = spawnedCount; Reason = reason ?? ""; IsReplay = isReplay;
        }
        public EncounterRequestResult AsReplay() => new EncounterRequestResult(RequestId, ExecutionSequence,
            SnapshotRevision, Status, PlannedCount, SpawnedCount, Reason, true, EnvironmentRevision);
    }
}

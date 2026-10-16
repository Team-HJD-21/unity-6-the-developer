// 요청의 확정 결과를 상관관계와 함께 전달합니다. 실패/부분 부작용 가능성도 재실행하지 않습니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class TurretCommandResult
    {
        public MatchId MatchId { get; }
        public Guid RequestId { get; }
        public TurretCommandKind Kind { get; }
        public EntityId TargetId { get; }
        public long ExecutionSequence { get; }
        public TurretCommandStatus Status { get; }
        public string FeatureCode { get; }
        public string Reason { get; }
        public bool IsReplay { get; }
        public bool WasAccepted => Status == TurretCommandStatus.Applied || Status == TurretCommandStatus.Accepted || Status == TurretCommandStatus.Unchanged;
        public TurretCommandResult(MatchId matchId, Guid requestId, TurretCommandKind kind, EntityId targetId,
            long executionSequence, TurretCommandStatus status, string featureCode = "", string reason = "", bool isReplay = false)
        {
            if (matchId.IsEmpty || requestId == Guid.Empty) throw new ArgumentException("A Match and request ID are required.");
            if (executionSequence < 0) throw new ArgumentOutOfRangeException(nameof(executionSequence));
            if (!Enum.IsDefined(typeof(TurretCommandKind), kind) || !Enum.IsDefined(typeof(TurretCommandStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if ((featureCode?.Length ?? 0) > 128 || (reason?.Length ?? 0) > 512) throw new ArgumentException("Result text exceeds its contract limit.");
            MatchId = matchId; RequestId = requestId; Kind = kind; TargetId = targetId.IsEmpty ? default : targetId;
            ExecutionSequence = executionSequence; Status = status; FeatureCode = featureCode ?? string.Empty;
            Reason = reason ?? string.Empty; IsReplay = isReplay;
        }
        public TurretCommandResult AsReplay() => new TurretCommandResult(MatchId, RequestId, Kind, TargetId,
            ExecutionSequence, Status, FeatureCode, Reason, true);
    }
}

// 접수/권한/중복 처리와 Feature 실행 결과를 구분합니다. Accepted는 장기 작업 완료가 아닙니다.
namespace TeamHJD.Game.Contracts
{
    public enum TurretCommandStatus
    {
        Applied, Accepted, Unchanged, FeatureRejected, TargetUnavailable, Unauthorized,
        DifferentMatch, MatchUnavailable, InProgress, RequestConflict, CapacityExceeded, ExecutionFailed
    }
}

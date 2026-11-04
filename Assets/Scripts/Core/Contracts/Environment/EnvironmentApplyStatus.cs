// 수신 환경의 적용 또는 거부 사유를 문자열 비교 없이 구분합니다.
namespace TeamHJD.Game.Contracts
{
    public enum EnvironmentApplyStatus
    {
        None, Applied, Unchanged, Disposed, Unauthorized, DifferentDescriptor,
        DifferentMatch, Stale, ConflictingRevision, ClockReversed
    }
}

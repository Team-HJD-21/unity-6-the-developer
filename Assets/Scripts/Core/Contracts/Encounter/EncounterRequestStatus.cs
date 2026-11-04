// 접수 거부와 계획·실제 생성 결과를 구분합니다.
namespace TeamHJD.Game.Contracts
{
    public enum EncounterRequestStatus
    {
        Spawned, PartialSpawn, PlanRejected, SpawnFailed, Unauthorized, DifferentMatch,
        MatchUnavailable, NotReady, InProgress, RequestConflict, CapacityExceeded, ExecutionFailed,
        OriginUnavailable, UnsupportedOrigin, UnsupportedDelivery
    }
}

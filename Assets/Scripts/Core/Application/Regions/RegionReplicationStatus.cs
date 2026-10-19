// 수신 지역 상태를 적용하지 않은 이유와 정상 중복을 구분합니다.
namespace TeamHJD.Game.Application
{
    public enum RegionReplicationStatus
    {
        Applied, Unchanged, Unauthorized, DifferentMatch, DifferentDescriptor, StaleGrid,
        StaleRevision, ConflictingGrid, ConflictingRevision
    }
}

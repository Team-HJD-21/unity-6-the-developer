// 현재 Match/Grid/revision 기준으로 지역 갱신이 적용됐는지 구분합니다.
namespace TeamHJD.Game.Application
{
    public enum RegionUpdateStatus { Applied, Unchanged, DifferentMatch, DifferentGrid, StaleRevision }
}

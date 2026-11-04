// 출입 보고의 접수 여부와 거부 이유입니다. 정지되었는지는 참가자 전체 상태로 따로 판단합니다.

namespace TeamHJD.Game.Domain
{
    public enum MatchPresenceReportStatus
    {
        Unavailable,
        Accepted,
        Unchanged,
        NotAuthority,
        InvalidLocation
    }
}

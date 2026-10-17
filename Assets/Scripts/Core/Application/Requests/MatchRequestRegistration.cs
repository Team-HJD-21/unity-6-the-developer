// 동일 요청의 신규 접수·진행·완료와 충돌·용량 거부를 구분합니다.
namespace TeamHJD.Game.Application
{
    public enum MatchRequestRegistration { Started, InProgress, Completed, Conflict, DifferentMatch, CapacityExceeded }
}

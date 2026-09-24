// Match Application 경계에서 명령을 처리한 결과 상태를 나타냅니다.

namespace TeamHJD.Game.Application
{
    public enum CommandSubmissionStatus
    {
        Accepted,
        Unauthorized,
        NotHandled,
        Rejected
    }
}

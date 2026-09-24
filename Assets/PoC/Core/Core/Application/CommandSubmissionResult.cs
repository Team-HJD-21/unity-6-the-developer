// 명령 처리 상태와 권한 거부 시 그 사유를 반환합니다.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class CommandSubmissionResult
    {
        public CommandSubmissionStatus Status { get; }
        public string Reason { get; }
        public SimulationOutcome Outcome { get; }

        internal CommandSubmissionResult(CommandSubmissionStatus status, string reason, SimulationOutcome outcome)
        {
            Status = status;
            Reason = reason ?? string.Empty;
            Outcome = outcome;
        }
    }
}

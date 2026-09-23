// Returns command routing status and the denial reason, if authority rejected it.

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

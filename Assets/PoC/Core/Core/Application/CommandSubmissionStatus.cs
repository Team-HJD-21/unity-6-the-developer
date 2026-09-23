// Describes the result of routing a command through the match application boundary.

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

// Immutable terminal result produced for a match.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchResult
    {
        public MatchId MatchId { get; }
        public MatchOutcome Outcome { get; }
        public int CompletedWave { get; }
        public DateTimeOffset CompletedAt { get; }

        public MatchResult(MatchId matchId, MatchOutcome outcome, int completedWave, DateTimeOffset completedAt)
        {
            MatchId = matchId;
            Outcome = outcome;
            CompletedWave = completedWave;
            CompletedAt = completedAt;
        }
    }
}

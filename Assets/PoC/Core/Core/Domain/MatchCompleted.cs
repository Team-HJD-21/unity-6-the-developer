// Records the final result already produced for a match.


namespace TeamHJD.Game.Domain
{
    public sealed class MatchCompleted : MatchEvent
    {
        public MatchResult Result { get; }

        public MatchCompleted(MatchId matchId, long sequence, MatchResult result)
            : base(matchId, sequence) => Result = result;
    }
}

// Base data for an immutable fact emitted by a match simulation step.


namespace TeamHJD.Game.Domain
{
    public abstract class MatchEvent
    {
        public MatchId MatchId { get; }
        public long Sequence { get; }

        protected MatchEvent(MatchId matchId, long sequence)
        {
            MatchId = matchId;
            Sequence = sequence;
        }
    }
}

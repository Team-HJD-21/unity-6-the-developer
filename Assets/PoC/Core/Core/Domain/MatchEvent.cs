// Base data for an immutable fact emitted by a match simulation step.


using System;

namespace TeamHJD.Game.Domain
{
    public abstract class MatchEvent
    {
        public MatchId MatchId { get; }
        public long Sequence { get; }

        protected MatchEvent(MatchId matchId, long sequence)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A match event requires a valid match ID.", nameof(matchId));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            MatchId = matchId;
            Sequence = sequence;
        }
    }
}

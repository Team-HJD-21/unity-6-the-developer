// Immutable state-transition output and facts to be published by the application layer.


using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class SimulationOutcome
    {
        public SimulationStatus Status { get; }
        public IReadOnlyList<MatchEvent> Events { get; }
        public MatchResult Result { get; }

        public SimulationOutcome(SimulationStatus status, IEnumerable<MatchEvent> events, MatchResult result = null)
        {
            Status = status;
            Events = new ReadOnlyCollection<MatchEvent>(new List<MatchEvent>(events ?? new MatchEvent[0]));
            Result = result;
        }
    }
}

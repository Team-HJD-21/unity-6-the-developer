// 상태 전이 결과와 Application 계층이 발행할 사실을 담는 불변 객체입니다.


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
            Events = new ReadOnlyCollection<MatchEvent>(CollectionCopy.CopyNonNull(events, nameof(events)));
            Result = result;
        }
    }
}

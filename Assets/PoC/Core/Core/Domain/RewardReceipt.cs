// Immutable reward proposal associated with a completed match result.


using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class RewardReceipt
    {
        public MatchId MatchId { get; }
        public MatchOutcome Outcome { get; }
        public IReadOnlyList<CurrencyDelta> CurrencyDeltas { get; }

        public RewardReceipt(MatchId matchId, MatchOutcome outcome, IEnumerable<CurrencyDelta> currencyDeltas)
        {
            MatchId = matchId;
            Outcome = outcome;
            CurrencyDeltas = new ReadOnlyCollection<CurrencyDelta>(new List<CurrencyDelta>(currencyDeltas ?? new CurrencyDelta[0]));
        }
    }
}

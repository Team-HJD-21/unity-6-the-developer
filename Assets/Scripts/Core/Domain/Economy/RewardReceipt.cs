// 완료된 Match 결과에 연결된 불변 보상 제안입니다.


using System.Collections.Generic;
using System.Collections.ObjectModel;

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class RewardReceipt
    {
        public MatchId MatchId { get; }
        public MatchOutcome Outcome { get; }
        public IReadOnlyList<CurrencyDelta> CurrencyDeltas { get; }

        public RewardReceipt(MatchId matchId, MatchOutcome outcome, IEnumerable<CurrencyDelta> currencyDeltas)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Reward receipt requires a valid match ID.", nameof(matchId));
            if (!Enum.IsDefined(typeof(MatchOutcome), outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
            MatchId = matchId;
            Outcome = outcome;
            CurrencyDeltas = new ReadOnlyCollection<CurrencyDelta>(CollectionCopy.CopyNonNull(currencyDeltas, nameof(currencyDeltas)));
        }
    }
}

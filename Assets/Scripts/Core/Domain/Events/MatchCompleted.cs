// Match에 대해 확정된 최종 결과와 제안 보상을 기록합니다.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchCompleted : MatchEvent
    {
        public MatchCompletion Completion { get; }
        public MatchResult Result => Completion.Result;
        public RewardReceipt RewardReceipt => Completion.RewardReceipt;

        public MatchCompleted(MatchId matchId, long sequence, MatchCompletion completion)
            : base(matchId, sequence)
        {
            Completion = completion ?? throw new ArgumentNullException(nameof(completion));
            if (Completion.Result.MatchId != matchId)
                throw new ArgumentException("Completion must belong to the event's match.", nameof(completion));
        }
    }
}

// Packages final match outcome and proposed rewards for profile or backend processing.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchCompletion
    {
        public MatchResult Result { get; }
        public RewardReceipt RewardReceipt { get; }

        public MatchCompletion(MatchResult result, RewardReceipt rewardReceipt)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            RewardReceipt = rewardReceipt ?? throw new ArgumentNullException(nameof(rewardReceipt));
            if (Result.MatchId != RewardReceipt.MatchId || Result.Outcome != RewardReceipt.Outcome)
                throw new ArgumentException("Result and reward receipt must describe the same match completion.", nameof(rewardReceipt));
        }
    }
}

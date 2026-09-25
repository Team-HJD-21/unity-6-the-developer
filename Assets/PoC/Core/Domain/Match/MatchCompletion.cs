// 최종 Match 결과와 제안 보상을 프로필 또는 Backend 처리용으로 묶습니다.

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

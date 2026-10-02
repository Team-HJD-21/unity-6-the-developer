// Initial_Stage의 Match lifecycle 검증에 사용하는 최소 모드 결과 정책입니다.
// 실제 승패·보상 규칙을 대신하지 않으며, PoC 조립자에만 주입합니다.

using System;
using Array = System.Array;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    internal sealed class InitialStagePocModeRules : IModeRules
    {
        public bool IsVictory(MatchState state) => false;
        public bool IsDefeat(MatchState state) => false;

        public MatchResult CreateResult(MatchState state, MatchOutcome outcome)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new MatchResult(state.MatchId, outcome, state.Wave.CurrentIndex, DateTimeOffset.UtcNow);
        }

        public RewardReceipt CreateRewardReceipt(MatchResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return new RewardReceipt(result.MatchId, result.Outcome, Array.Empty<CurrencyDelta>());
        }
    }
}

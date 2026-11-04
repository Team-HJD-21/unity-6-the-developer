// 스토리 Match의 실제 CU 파괴를 패배로 판단하고 불변 결과를 만듭니다.
// 보스 게이트·승리/저장·보상 수치는 미정이며 임의 정책을 추가하지 않습니다.
using System;

namespace TeamHJD.Game.Domain
{
    public sealed class StoryModeRules : IModeRules, IMatchProgressRules
    {
        private readonly Func<DateTimeOffset> _utcNow;
        public StoryModeRules(Func<DateTimeOffset> utcNow = null) => _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        // 기존 State 조회는 확정 결과만 읽습니다. 실제 HP 판단은 Evaluate의 Feature 관측값을 사용합니다.
        public bool IsVictory(MatchState state) => state?.Result?.Outcome == MatchOutcome.Victory;
        public bool IsDefeat(MatchState state) => state?.Result?.Outcome == MatchOutcome.Defeat;
        public MatchOutcome? Evaluate(MatchProgressSnapshot progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            return progress.ControlUnitHealth == 0 ? MatchOutcome.Defeat : (MatchOutcome?)null;
        }
        public MatchResult CreateResult(MatchState state, MatchOutcome outcome)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new MatchResult(state.MatchId, outcome, state.Wave.CurrentIndex, _utcNow());
        }
        public RewardReceipt CreateRewardReceipt(MatchResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return new RewardReceipt(result.MatchId, result.Outcome, Array.Empty<CurrencyDelta>());
        }
    }
}

// 스토리 Match의 실제 CU 파괴를 패배로 판단하고 불변 결과를 만듭니다.
// 명시적으로 확정된 승패의 결과/보상 형식을 제공하며 보스 사망을 자동 승리로 판정하지 않습니다.
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
            return new MatchResult(state.MatchId, outcome, 0, _utcNow());
        }
        public RewardReceipt CreateRewardReceipt(MatchResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return new RewardReceipt(result.MatchId, result.Outcome, Array.Empty<CurrencyDelta>());
        }
    }
}

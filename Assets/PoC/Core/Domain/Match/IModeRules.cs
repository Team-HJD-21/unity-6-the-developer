// 모드별 결과·보상 정책을 Match 시뮬레이션에 제공합니다.


namespace TeamHJD.Game.Domain
{
    public interface IModeRules
    {
        bool IsVictory(MatchState state);
        bool IsDefeat(MatchState state);
        MatchResult CreateResult(MatchState state, MatchOutcome outcome);
        RewardReceipt CreateRewardReceipt(MatchResult result);
    }
}

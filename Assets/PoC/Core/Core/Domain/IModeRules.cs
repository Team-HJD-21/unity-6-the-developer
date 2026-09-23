// Supplies mode-specific result and reward policy to the match simulation.


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

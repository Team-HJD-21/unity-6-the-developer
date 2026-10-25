// 실제 Feature 관측값에 따른 모드 완료 판단을 제공합니다.
// null은 계속 진행이며 UI·Scene 전환이나 Feature 상태 변경을 수행하지 않습니다.
namespace TeamHJD.Game.Domain
{
    public interface IMatchProgressRules
    {
        MatchOutcome? Evaluate(MatchProgressSnapshot progress);
    }
}

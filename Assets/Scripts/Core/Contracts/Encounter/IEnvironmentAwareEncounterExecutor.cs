// 기존 Encounter 실행 경로가 권위 측에서 조회한 환경을 실제 정책에 소비합니다.
// 구체 Planner·밸런스 데이터·생성은 Enemy Feature가 소유합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEnvironmentAwareEncounterExecutor : IEncounterExecutor
    {
        EncounterRequestResult Execute(EncounterRequest request, BattlefieldSpatialSnapshot battlefield,
            MatchEnvironmentSnapshot environment, long executionSequence);
    }
}

// 포인트 밖 생성 의도를 실제 Enemy 실행 경계에 전달합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IOriginAwareEncounterExecutor : IEnvironmentAwareEncounterExecutor
    {
        EncounterRequestResult Execute(EncounterRequest request, BattlefieldSpatialSnapshot battlefield,
            MatchEnvironmentSnapshot environment, EncounterSpawnPlacement placement, long executionSequence);
    }
}

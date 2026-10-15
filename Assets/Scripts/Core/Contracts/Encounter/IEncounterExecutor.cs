// A의 요청 검증 뒤 E가 소유한 계획·실제 생성 경로를 실행합니다.
// 이 Port 구현은 연결을 시작하지 않으며 부분 실행 결과도 반환해야 합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterExecutor
    {
        EncounterRequestResult Execute(EncounterRequest request, BattlefieldSpatialSnapshot snapshot, long executionSequence);
    }
}

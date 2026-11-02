// D 생성자가 실제 등록을 끝낸 개체를 명시적 Match 논리 ID에 연결하는 Port입니다.
// 삭제는 D의 실제 해제 통지를 따르며 Core가 별도 생성자/매니저를 만들지 않습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretEntityRegistration : ITurretEntityIdResolver
    {
        bool TryRegisterEntity(MatchId matchId, EntityId entityId, int localInstanceId, out string reason);
    }
}

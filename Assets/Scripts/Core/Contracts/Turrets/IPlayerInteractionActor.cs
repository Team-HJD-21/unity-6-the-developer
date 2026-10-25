// Host가 실제 소유하는 Player actor의 조작 상태 조회 Port입니다.
// Client의 명령/좌표 패킷을 이 구현체로 바로 연결하지 않습니다.
namespace TeamHJD.Game.Contracts
{
    public interface IPlayerInteractionActor
    {
        bool TryCaptureInteraction(out PlayerInteractionSnapshot snapshot);
    }
}

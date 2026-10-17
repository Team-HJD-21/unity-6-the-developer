// Unity의 프레임 루프와 실제 전송 큐를 연결하는 최소 Port입니다.
// 게임시간 정지와 무관하게 호출하며 구체 패킷 예산은 전송 어댑터가 관리합니다.
namespace TeamHJD.Game.Contracts
{
    public interface INetworkMessagePump
    {
        void Pump();
    }
}

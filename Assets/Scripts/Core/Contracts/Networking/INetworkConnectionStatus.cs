// Feature가 읽는 실제 연결 상태입니다. 연결 개설·종료 권한은 제공하지 않습니다.
// NGO 같은 구체 SDK는 이 계약의 구현체가 감추며 값은 현재 연결을 그대로 반영합니다.
namespace TeamHJD.Game.Contracts
{
    public interface INetworkConnectionStatus
    {
        bool IsListening { get; }
        bool IsServer { get; }
        bool IsClient { get; }
        bool IsHost { get; }
        /// <summary>서버에 실제 연결된 Client 수입니다. Host 자신을 포함하며 미접속/Client에서는 0입니다.</summary>
        int ConnectedClientCount { get; }
    }
}

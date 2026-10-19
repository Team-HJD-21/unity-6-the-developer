// App이 소유하는 명시적 연결 시작·종료 경계입니다.
// 시작 성공은 연결 요청 수락이며 Scene/Feature 준비나 Match 시작을 뜻하지 않습니다.
using System;

namespace TeamHJD.Game.Contracts
{
    public interface INetworkSession : IDisposable
    {
        NetworkConnectionState State { get; }
        bool IsServer { get; }
        ulong LocalConnectionId { get; }
        event Action<NetworkConnectionState> StateChanged;
        event Action<ulong> PeerConnected;
        event Action<ulong> PeerDisconnected;
        bool TryStartHost();
        bool TryStartClient();
        void Shutdown();
    }
}

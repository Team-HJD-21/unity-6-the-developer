// 실제 연결 발신자와 타입 메시지를 Application에 전달하는 Port입니다.
// 구체 직렬화/패킷 전송은 Infrastructure가 소유하며 Domain에 NGO를 노출하지 않습니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface IMatchLifecycleTransport
    {
        event Action<ulong, NetworkMatchDescriptor> DescriptorReceived;
        event Action<ulong, MatchLifecycleSignalKind, MatchLifecycleSignal> SignalReceived;
        bool TrySendDescriptor(ulong connectionId, NetworkMatchDescriptor descriptor);
        bool TrySendSignal(ulong connectionId, MatchLifecycleSignalKind kind, MatchLifecycleSignal signal);
    }
}

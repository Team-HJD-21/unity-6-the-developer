// 실제 발신자와 환경 전체 상태를 전달합니다. false는 큐 미접수이며 상위 소유자가 재시도합니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface IMatchEnvironmentTransport
    {
        event Action<ulong, EnvironmentReplicationFrame> FrameReceived;
        bool TrySendFrame(ulong connection, EnvironmentReplicationFrame frame);
    }
}

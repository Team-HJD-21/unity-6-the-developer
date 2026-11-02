// 실제 서버 발신자와 확정 지역 프레임을 전달하는 읽기 복제 Port입니다.
// 전송 결과 false는 큐 미접수이며 상위 소유자가 최신 전체 상태로 재시도합니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface IRegionSnapshotTransport
    {
        event Action<ulong, RegionReplicationFrame> FrameReceived;
        bool TrySendFrame(ulong connectionId, RegionReplicationFrame frame);
    }
}

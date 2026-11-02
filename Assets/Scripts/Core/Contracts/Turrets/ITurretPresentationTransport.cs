// Host 확정 전체 HUD 투영의 전달 Port입니다. Client는 이 경계로 상태를 제출하지 않습니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretPresentationTransport
    {
        event Action<ulong, TurretPresentationFrame> FrameReceived;
        bool TrySendFrame(ulong connectionId, TurretPresentationFrame frame);
    }
}

// HUD가 현재 Match의 Host 확정 투영만 읽는 Port입니다. 아직 수신 전이면 Snapshot은 null입니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretPresentationSource
    {
        TurretPresentationFrame Snapshot { get; }
        event Action<TurretPresentationFrame> Changed;
    }
}

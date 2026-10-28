// Host가 확정한 UI용 상태 표식입니다. Client의 D 런타임을 변경하는 지시가 아닙니다.
using System;
namespace TeamHJD.Game.Contracts
{
    [Flags]
    public enum TurretPresentationFlags
    {
        None = 0, Initialized = 1, Activated = 2, Operational = 4,
        Destroyed = 8, Locked = 16, ManualOperationBlocked = 32
    }
}

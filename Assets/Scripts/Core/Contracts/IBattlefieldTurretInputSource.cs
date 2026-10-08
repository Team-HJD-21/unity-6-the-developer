// Scene 조립 계층이 터렛 위치를 Unity 독립 공간 입력으로 받을 때 사용하는 계약입니다.
// LayoutChanged는 Core에 전달할 실제 공간 입력이 달라졌을 때만 발생합니다.

using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IBattlefieldTurretInputSource
    {
        event Action LayoutChanged;
        BattlefieldSpatialInput CaptureTurretLayout();
    }
}

// 현재 시뮬레이션 골격이 명령을 처리했는지 나타냅니다.

namespace TeamHJD.Game.Domain
{
    public enum SimulationStatus
    {
        NotHandled,
        Applied,
        Rejected
    }
}

// Weather가 낮밤의 정상 진행 신호만 받아 다음 날씨를 선택하는 접점입니다.
// 복원·Client 적용은 Changed만 통지하며 새 날씨 추첨을 요청하지 않습니다.
using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IMatchDayNightCycle : IMatchTimeOfDaySource
    {
        event Action PhaseAdvanced;
        DayNightPhase Phase { get; }

        double DaySeconds { get; }
        double NightSeconds { get; }
        double RemainingSeconds { get; }
    }
}

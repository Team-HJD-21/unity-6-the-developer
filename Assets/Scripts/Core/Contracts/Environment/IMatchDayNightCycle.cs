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
        /// <summary>HUD가 읽는 남은 게임 초입니다. Client는 권위 구간·같은 세대 시계 수신 전 NaN이며 자체 전환하지 않습니다.</summary>
        double RemainingSeconds { get; }
    }
}

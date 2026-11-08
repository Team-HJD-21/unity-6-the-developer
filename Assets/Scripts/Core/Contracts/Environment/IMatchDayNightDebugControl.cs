// 디버그 UI가 Host의 현재 낮밤 타이머만 다시 시작하는 경계입니다.
// 공용 Match 시각·다른 예약·날씨 추첨/큐는 변경하지 않습니다.
namespace TeamHJD.Game.Contracts
{
    public interface IMatchDayNightDebugControl
    {
        bool CanRestartCurrentPhase { get; }
        /// <summary>현재 Day/Night를 유지하고 해당 구간의 남은 시간을 처음으로 돌립니다. Client/종료된 Match는 거부합니다.</summary>
        bool TryRestartCurrentPhase();
    }
}

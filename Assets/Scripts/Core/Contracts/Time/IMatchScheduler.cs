// Feature가 게임 시간 기준 단발/반복 작업을 등록하는 계약입니다.
// 반환 lease 또는 취소 토큰으로 해제하며 실행은 Match 소유자의 Pump에서만 이뤄집니다.
using System;
using System.Threading;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IMatchScheduler
    {
        /// <summary>Weather 등 Feature가 지정한 게임 초 뒤 한 번 실행할 작업을 등록합니다.</summary>
        /// <returns>Feature 해제 시 Dispose할 예약 lease입니다. 지연 0도 등록 중에 즉시 호출하지 않습니다.</returns>
        IDisposable Schedule(double delaySeconds, Action<MatchTimerTick> callback, CancellationToken cancellation = default);
        /// <summary>지정한 양의 게임 초 간격으로 실행합니다. 첫 호출은 한 간격 뒤이며 밀린 회차는 한 통지로 합칩니다.</summary>
        /// <returns>Feature 해제 시 Dispose할 예약 lease입니다. 미실행 회차 수는 callback 인자로 전달합니다.</returns>
        IDisposable ScheduleRepeating(double intervalSeconds, Action<MatchTimerTick> callback, CancellationToken cancellation = default);
    }
}

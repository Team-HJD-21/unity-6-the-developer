// 예약 callback에 예정/실행 게임 시각과 합쳐진 미실행 회차를 전달합니다.
// 프레임 수나 실제 네트워크 시간과 구분해 Feature의 지연 전이를 판단하게 합니다.
namespace TeamHJD.Game.Domain
{
    public readonly struct MatchTimerTick
    {
        public double ScheduledAt { get; }
        public double ExecutedAt { get; }
        public long MissedIntervals { get; }
        public MatchTimerTick(double scheduledAt, double executedAt, long missedIntervals)
        { ScheduledAt = scheduledAt; ExecutedAt = executedAt; MissedIntervals = missedIntervals; }
    }
}

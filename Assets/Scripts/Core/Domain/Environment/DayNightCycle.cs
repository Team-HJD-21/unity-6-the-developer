// 게임 경과 시각에서 낮밤 구간과 다음 경계를 계산합니다.
// 프레임별 deltaTime을 누적하지 않으며 기본 한 주기는 낮300초+밤300초입니다.
using System;

namespace TeamHJD.Game.Domain
{
    public sealed class DayNightCycle
    {
        public double DaySeconds { get; }
        public double NightSeconds { get; }
        public double EpochSeconds { get; }
        public DayNightPhase InitialPhase { get; }

        public DayNightCycle(double daySeconds = 300, double nightSeconds = 300, double epochSeconds = 0,
            DayNightPhase initialPhase = DayNightPhase.Day)
        {
            if (!double.IsFinite(daySeconds) || daySeconds <= 0 || !double.IsFinite(nightSeconds) || nightSeconds <= 0 || !double.IsFinite(daySeconds + nightSeconds) || !double.IsFinite(epochSeconds) || epochSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(daySeconds), "Use positive finite phase lengths and a nonnegative epoch.");
            if (!Enum.IsDefined(typeof(DayNightPhase), initialPhase))
                throw new ArgumentOutOfRangeException(nameof(initialPhase));
            DaySeconds = daySeconds;
            NightSeconds = nightSeconds;
            EpochSeconds = epochSeconds;
            InitialPhase = initialPhase;
        }

        public DayNightPhase GetPhase(double gameSeconds) => Position(gameSeconds) < DaySeconds ? DayNightPhase.Day : DayNightPhase.Night;
        public double GetRemainingSeconds(double gameSeconds)
        {
            double position = Position(gameSeconds);
            return (position < DaySeconds ? DaySeconds : DaySeconds + NightSeconds) - position;
        }

        /// <summary>지연 프레임에서도 실제 도달한 구간을 식별합니다. 지나간 구간마다 날씨를 연속 추첨하지 않습니다.</summary>
        public long GetTransitionIndex(double gameSeconds)
        {
            double elapsed = Elapsed(gameSeconds) + (InitialPhase == DayNightPhase.Night ? DaySeconds : 0);
            double cycles = Math.Floor(elapsed / (DaySeconds + NightSeconds));
            if (cycles >= long.MaxValue / 2d)
                throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            return checked((long)cycles * 2 + (GetPhase(gameSeconds) == DayNightPhase.Night ? 1 : 0)
                - (InitialPhase == DayNightPhase.Night ? 1 : 0));
        }

        private double Position(double gameSeconds) =>
            (Elapsed(gameSeconds) + (InitialPhase == DayNightPhase.Night ? DaySeconds : 0)) % (DaySeconds + NightSeconds);
        private double Elapsed(double gameSeconds)
        {
            if (!double.IsFinite(gameSeconds) || gameSeconds < EpochSeconds)
                throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            return gameSeconds - EpochSeconds;
        }
    }
}

// 확보·상실 판정의 조절 가능한 시험값입니다. 값의 단위와 의미를 한곳에서 검증합니다.
using System;

namespace TeamHJD.Game.Domain
{
    public sealed class StaticRegionCaptureSettings
    {
        public double HoldSeconds { get; }
        public double AbandonSeconds { get; }
        public double LossSeconds { get; }
        public double MinimumTeamDamage { get; }
        public int EnemiesPerParticipant { get; }

        public StaticRegionCaptureSettings(double holdSeconds = 60, double abandonSeconds = 180,
            double lossSeconds = 30, double minimumTeamDamage = 20, int enemiesPerParticipant = 2)
        {
            foreach (double value in new[] { holdSeconds, abandonSeconds, lossSeconds, minimumTeamDamage })
                if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(holdSeconds));
            if (enemiesPerParticipant < 0) throw new ArgumentOutOfRangeException(nameof(enemiesPerParticipant));
            HoldSeconds = holdSeconds; AbandonSeconds = abandonSeconds; LossSeconds = lossSeconds;
            MinimumTeamDamage = minimumTeamDamage; EnemiesPerParticipant = enemiesPerParticipant;
        }
    }
}

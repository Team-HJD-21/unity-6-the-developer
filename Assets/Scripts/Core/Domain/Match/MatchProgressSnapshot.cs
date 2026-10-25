// 모드가 판단할 실제 Feature 관측값을 불변 자료로 전달합니다.
// 기존 MatchState의 초기 HP를 실제 CU HP로 취급하지 않습니다.
using System;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchProgressSnapshot
    {
        public MatchId MatchId { get; }
        public int ControlUnitHealth { get; }
        public int ControlUnitMaxHealth { get; }
        public double ElapsedSeconds { get; }

        public MatchProgressSnapshot(MatchId matchId, int health, int maxHealth, double elapsedSeconds)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A Match ID is required.", nameof(matchId));
            if (maxHealth <= 0 || health < 0 || health > maxHealth) throw new ArgumentOutOfRangeException(nameof(health));
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            MatchId = matchId; ControlUnitHealth = health; ControlUnitMaxHealth = maxHealth; ElapsedSeconds = elapsedSeconds;
        }
    }
}

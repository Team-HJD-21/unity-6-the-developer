// 구조 입력의 시험값입니다. Scene 조립 때 검증/복사하며 같은 Match 중 암묵적으로 바꾸지 않습니다.
using System;
namespace TeamHJD.Game.Domain
{
    [Serializable]
    public sealed class MissionRecoverySettings
    {
        public double holdSeconds = 3;
        public double healthRatio = .3;
        public double distance = 3;
        public double inputExpirySeconds = .5;
        public bool IsValid => Positive(holdSeconds) && Positive(healthRatio) && healthRatio <= 1 &&
            Positive(distance) && Positive(inputExpirySeconds);
        private static bool Positive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}

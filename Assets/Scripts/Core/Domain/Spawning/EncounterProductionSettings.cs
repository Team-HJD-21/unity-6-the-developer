// Match가 정규 Encounter를 요청하는 간격과 요청당 마릿수 상한입니다.
// 예산·공간 배분·편성은 Enemy Planner가 결정하며 이 설정은 생존 적 전체 상한이 아닙니다.
using System;

namespace TeamHJD.Game.Domain
{
    [Serializable]
    public sealed class EncounterProductionSettings
    {
        public bool Enabled = true;
        public double FirstDelaySeconds = 0.5;
        public double IntervalSeconds = 15;
        public int MaximumEnemiesPerRequest = 500;

        public bool IsValid => double.IsFinite(FirstDelaySeconds) && FirstDelaySeconds >= 0 &&
            double.IsFinite(IntervalSeconds) && IntervalSeconds > 0 && MaximumEnemiesPerRequest > 0;

        /// <summary>조립부가 제작 설정을 현재 Match의 독립 설정으로 복사합니다.</summary>
        public EncounterProductionSettings Copy() => new EncounterProductionSettings
        {
            Enabled = Enabled, FirstDelaySeconds = FirstDelaySeconds,
            IntervalSeconds = IntervalSeconds, MaximumEnemiesPerRequest = MaximumEnemiesPerRequest
        };
    }
}

// 실행 직전 권위 측에서 해석한 위치를 원래 origin과 함께 전달합니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class EncounterSpawnPlacement
    {
        public EncounterSpawnOrigin Origin { get; }
        public SpawnArea Area { get; }
        public EncounterSpawnPlacement(EncounterSpawnOrigin origin, SpawnArea area = null)
        {
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            if (origin.Kind != SpawnOriginKind.AuthoredPoint && area == null)
                throw new ArgumentException("A resolved world placement is required.", nameof(area));
            Area = area;
        }
    }
}

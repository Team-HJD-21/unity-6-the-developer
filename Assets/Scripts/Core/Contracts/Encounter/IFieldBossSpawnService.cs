// Stage 진행 조율부가 33/66% Field 사건을 실제 Enemy 생성 경계에 요청합니다.
// 구현자는 event ID별 생성 결과를 재사용하고 점유 밖의 유효 위치·실제 개체 생성을 보장합니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IFieldBossSpawnService
    {
        /// <summary>Host가 호출합니다. true는 실제 생성된 개체 ID가 있을 때만 반환하며 처치는 요구하지 않습니다.</summary>
        /// <remarks>부분 성공·응답 유실 뒤 같은 event ID의 재호출로 중복 생성하지 않습니다. 일반 적 대체는 허용하지 않습니다.</remarks>
        bool TrySpawnFieldBoss(Guid eventId, int thresholdPercent, RegionGridSnapshot regions,
            out EntityId actor, out string reason);
    }
}

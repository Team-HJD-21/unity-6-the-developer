// World/source 생성 의도를 공통 권한·요청 경계로 전달하는 Feature 호출 창구입니다.
// 지연은 Match 게임 시간을 사용하며 생성 시점에 최신 공간/환경/source를 다시 조회합니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterOriginRequestTarget
    {
        int PendingSpawnRequests { get; }
        EncounterRequestResult RequestOrigin(string preset, int maximum, EncounterRequestSource source, EncounterSpawnOrigin origin, Guid cause);
        bool TryScheduleOrigin(string preset, int maximum, EncounterRequestSource source, EncounterSpawnOrigin origin, Guid cause,
            double delayGameSeconds, out Guid requestId);
    }
}

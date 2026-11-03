// App 연결을 사용하는 Encounter 소비자에 권한 참가자와 Match 범위를 명시합니다.
// 적 생성·제거는 Feature가 수행하고 연결 개설·종료는 App 소유자만 수행합니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IAppOwnedEncounterMatchConsumer : IEncounterMatchConsumer
    {
        void UseAppNetworkOwnership();
        void BindAuthorityMatch(MatchId matchId, PlayerId authorityPlayerId,
            Func<BattlefieldSpatialSnapshot> snapshotProvider);
    }
}

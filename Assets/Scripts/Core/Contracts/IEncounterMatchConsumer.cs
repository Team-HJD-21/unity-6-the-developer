// Scene의 Encounter 구현에 Match 식별자와 최신 Battlefield 조회 경로를 전달합니다.
// Core는 Enemy 구현이나 네트워크 생성 방식을 직접 참조하지 않습니다.

using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IEncounterMatchConsumer
    {
        void BindMatch(MatchId matchId, Func<BattlefieldSpatialSnapshot> snapshotProvider);
        void UnbindMatch(MatchId matchId);
    }
}

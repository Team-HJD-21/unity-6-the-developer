// Scene 조립부가 검증한 로컬 참가자와 actor를 UI/표현에 명시적으로 전달합니다.
// 소비자는 actor를 생성·파괴하지 않으며 이전 Match의 해제 요청으로 새 바인딩을 지우지 않습니다.
using TeamHJD.Game.Domain;
using UnityEngine;
namespace TeamHJD.Game.Presentation.Scene
{
    public interface ILocalPlayerActorConsumer
    {
        void PrepareLocalActor(MatchId matchId, PlayerId playerId);
        void BindLocalActor(MatchId matchId, PlayerId playerId, GameObject actor);
        void UnbindLocalActor(MatchId matchId);
    }
}

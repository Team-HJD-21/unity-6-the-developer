// Scene 조립부가 HUD에 Match별 요청 경계와 Host 확정 표시 상태를 함께 주입합니다.
// 해제는 같은 Match에만 적용하며 Client 로컬 D 상태로 되돌아가지 않습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretCommandConsumer
    {
        void BindTurretCommands(MatchId matchId, PlayerId playerId, ITurretCommandRequester requester, ITurretPresentationSource presentation);
        void UnbindTurretCommands(MatchId matchId);
    }
}

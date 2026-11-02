// 명령 검증 순간의 권위 Player 상태를 조회합니다. 제공되지 않은 참가자는 실패로 닫습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IPlayerInteractionSource
    {
        bool TryCapture(MatchId matchId, PlayerId playerId, out PlayerInteractionSnapshot snapshot);
    }
}

// Client actor가 로컬 권한 MatchSession 없이 공유 Match 수명·소유자를 전달받는 Port입니다.
// 이동/공격 복제는 B Feature의 별도 구현 책임입니다.
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Presentation.Scene
{
    public interface IPlayerNetworkMatchBinding
    {
        void BindNetworkMatch(NetworkMatchCoordinator match, PlayerId localPlayerId);
        void UnbindNetworkMatch();
    }
}

// Scene 조립부가 로컬 Player 입력에 현재 Match의 구조 요청 서비스를 연결합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IPlayerRescueConsumer
    {
        void BindRescue(PlayerId player, IPlayerRescueService service);
        void UnbindRescue();
    }
}

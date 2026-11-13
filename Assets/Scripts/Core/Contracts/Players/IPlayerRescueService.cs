// Player 입력과 Host 구조 조율부의 요청 경계입니다. 원격 구현은 송신자 검증을 먼저 수행합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IPlayerRescueService
    {
        bool IsReady { get; }
        bool TryFindTarget(PlayerId rescuer, out PlayerId target);
        bool SetRescueHeld(PlayerId rescuer, PlayerId target, bool held);
        double HoldProgress(PlayerId rescuer);
    }
}

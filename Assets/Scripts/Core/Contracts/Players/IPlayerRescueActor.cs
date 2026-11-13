// Core의 구조 조율부가 실제 Host Player 생존/위치와 복귀를 사용하는 port입니다.
// 입력 패킷의 좌표/HP 주장을 원본으로 사용하지 않습니다.
namespace TeamHJD.Game.Contracts
{
    public interface IPlayerRescueActor
    {
        bool TryCaptureRescue(out PlayerInteractionSnapshot snapshot);
        bool TryRevive(double maximumHealthRatio);
    }
}

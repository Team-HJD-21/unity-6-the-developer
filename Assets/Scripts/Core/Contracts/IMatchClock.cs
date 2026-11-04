// 실행 중 Match의 누적 게임 시간만 읽습니다. 실시간/네트워크 pump 시계와 다릅니다.
namespace TeamHJD.Game.Contracts
{
    public interface IMatchClock
    {
        double ElapsedSeconds { get; }
    }
}

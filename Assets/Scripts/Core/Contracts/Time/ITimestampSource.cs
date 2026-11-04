// 엔진/네트워크가 제공하는 초 단위 원본 시각과 준비 여부를 Core에 전달합니다.
// 게임 정지·배속은 원본을 변경하지 않고 MatchClock에서 적용합니다.
namespace TeamHJD.Game.Contracts
{
    public interface ITimestampSource
    {
        /// <summary>MatchClock이 원본 시각을 읽습니다. 준비 전이면 false이며 0을 유효 시각으로 추정하지 않습니다.</summary>
        bool TryReadSeconds(out double seconds);
    }
}

// Feature와 진단이 공유하는 Match 게임 시간의 읽기 경계입니다.
// 제어는 권위 조립부가 소유하며 소비자는 시계나 SDK 시간을 직접 변경하지 않습니다.
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IMatchTime : IMatchClock
    {
        /// <summary>마지막 Pump에서 유효한 원본 시각을 읽었는지 나타냅니다.</summary>
        bool IsAvailable { get; }
        /// <summary>정지·배속·복원 때 확정한 불변 시각 기준입니다. 현재 경과 시간은 ElapsedSeconds로 읽습니다.</summary>
        MatchTimeSnapshot Snapshot { get; }
    }
}

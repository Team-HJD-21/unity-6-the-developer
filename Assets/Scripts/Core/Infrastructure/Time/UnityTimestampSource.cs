// 네트워크가 없는 Feature/Editor 실행에서 Unity의 double 실시간을 제공합니다.
// TimeScale의 영향을 받지 않으며 게임 정지·배속은 MatchClock이 적용합니다.
using TeamHJD.Game.Contracts;
using UnityEngine;

namespace TeamHJD.Game.Infrastructure
{
    public sealed class UnityTimestampSource : ITimestampSource
    {
        /// <inheritdoc />
        public bool TryReadSeconds(out double seconds)
        { seconds = Time.realtimeSinceStartupAsDouble; return true; }
    }
}

// 공용 예약 서비스를 사용하는 Weather/낮밤 Feature의 선택적 확장 계약입니다.
// 기존 독립 Feature 바인딩은 유지하며 조립부가 실제 scheduler 제공 경로를 명시합니다.
using System;
using System.Threading;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IMatchScheduledEnvironmentFeature : IMatchEnvironmentFeature
    {
        /// <summary>Scene 조립부가 Host의 시계·예약·seed·권한·취소 수명을 한 번 연결합니다.</summary>
        /// <returns>Feature의 예약과 바인딩을 함께 해제하는 lease입니다. Scene 소유자가 Dispose합니다.</returns>
        IDisposable BindAuthority(MatchId match, IMatchClock clock, IMatchScheduler scheduler,
            int seed, Func<bool> canWrite, CancellationToken gameplay);
    }
}

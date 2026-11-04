// World Feature의 환경 진행/표현을 현재 Match의 Host 또는 읽기 replica에 임대합니다.
// 미바인딩 Client가 별도 Random/타이머로 권위 값을 만들지 않게 합니다.
using System;
using System.Threading;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IMatchEnvironmentFeature
    {
        event Action Changed;
        void RequireMatchScope();
        IDisposable BindAuthority(MatchId match, IMatchClock clock, int seed, Func<bool> canWrite, CancellationToken gameplay);
        IDisposable BindReplica(MatchId match);
        void ApplyReplica(MatchEnvironmentSnapshot snapshot);
    }
}

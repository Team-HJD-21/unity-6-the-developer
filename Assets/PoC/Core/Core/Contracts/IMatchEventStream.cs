// 이벤트 발행 권한을 주지 않고 구독자에게 Match 사실을 공개합니다.

using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IMatchEventStream
    {
        IDisposable Subscribe(Action<MatchEvent> listener);
    }
}

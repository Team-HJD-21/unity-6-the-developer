// 확정 환경의 현재 값과 변경 사실을 조회합니다. 구독자는 자기 수명에 맞춰 해제합니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IMatchEnvironmentSource
    {
        MatchEnvironmentSnapshot Snapshot { get; }
        event Action<MatchEnvironmentSnapshot> Changed;
    }
}

// Feature의 동적 source 등록 수명을 나타냅니다. 해제는 대기 요청만 취소하고 이미 생성한 적을 삭제하지 않습니다.
using System;
using System.Threading;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterSpawnSourceLease : IDisposable
    {
        SpawnSourceReference Reference { get; }
        CancellationToken Cancellation { get; }
        bool TryUpdate(SpawnArea area);
    }
}

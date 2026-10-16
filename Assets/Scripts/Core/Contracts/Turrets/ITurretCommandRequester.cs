// HUD/입력은 로컬 D 상태를 변경하지 않고 요청 수락과 서버 확정 결과를 구분합니다.
// 응답을 받지 못하면 같은 GUID의 TryRetry만 사용하며 새 GUID로 자동 재실행하지 않습니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretCommandRequester
    {
        event Action<TurretCommandResult> ResultReceived;
        int PendingCount { get; }
        bool TrySubmit(TurretCommandRequest request);
        bool TryRetry(Guid requestId);
    }
}

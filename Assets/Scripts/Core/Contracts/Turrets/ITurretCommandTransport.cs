// 검증된 연결 번호와 준비 세대를 보존하는 터렛 명령/결과 전송 Port입니다.
// 전송 수락은 실행 성공이 아닙니다. 인증·제품 정책·중복 실행 방지는 별도 경계입니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretCommandTransport
    {
        event Action<ulong, Guid, TurretCommandRequest> RequestReceived;
        event Action<ulong, Guid, TurretCommandResult> ResultReceived;
        bool TrySendRequest(ulong connectionId, Guid descriptorId, TurretCommandRequest request);
        bool TrySendResult(ulong connectionId, Guid descriptorId, TurretCommandResult result);
    }
}

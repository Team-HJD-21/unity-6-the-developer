// HUD/입력의 터렛 의도를 Host 검증 경로에 전달하고 확정 결과를 구독하게 합니다.
// 응답을 받지 못하면 같은 GUID의 TryRetry만 사용하며 새 GUID로 자동 재실행하지 않습니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretCommandRequester
    {
        /// <summary>HUD가 요청별 확정 결과를 받습니다. Host는 제출 함수가 반환되기 전에 통지할 수 있습니다.</summary>
        /// <remarks>입력/화면을 바인딩할 때 먼저 구독하고 해당 Match 또는 화면 해제 시 구독을 제거합니다.</remarks>
        event Action<TurretCommandResult> ResultReceived;
        /// <summary>Client가 전송했지만 아직 확정 응답을 받지 못한 요청 수입니다.</summary>
        int PendingCount { get; }
        /// <summary>실행 중 Match에서 Player/UI가 공용 터렛 명령을 제출합니다.</summary>
        /// <returns>Host 처리 또는 Client 전송 경로에서 접수했으면 true입니다. 실제 수행 판정은 ResultReceived로 확인합니다.</returns>
        /// <remarks>결과 구독을 먼저 연결합니다. 실패 응답과 응답 대기를 구분하고 재전송에 같은 요청 ID를 사용합니다.</remarks>
        bool TrySubmit(TurretCommandRequest request);
        /// <summary>Client가 아직 대기 중인 요청을 같은 ID·내용으로 다시 전송합니다.</summary>
        /// <returns>현재 Match의 대기 요청을 다시 전송했으면 true입니다. 새 명령을 생성하지 않습니다.</returns>
        bool TryRetry(Guid requestId);
    }
}

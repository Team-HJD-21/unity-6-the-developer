// Feature가 사전 검증과 백업 준비를 마친 복원 작업입니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface ICheckpointRestoreStep : IDisposable
    {
        /// <summary>실행기가 사전 검증을 모두 마친 뒤 소유 스레드에서 적용합니다.</summary>
        void Apply();
        /// <summary>시간을 원복한 뒤 백업을 복구합니다. 전역 시간 복원이 예약을 취소하므로 Apply 전 단계에도 호출될 수 있습니다.</summary>
        void Rollback();
        // Dispose는 임시 자료/lease만 해제합니다. 결과 게시나 추가 게임 상태 변경은 수행하지 않습니다.
    }
}

// 저장 실행기가 게임 상태를 임의 소유하지 않고 현재 권위 Match의 시간/수명 경계를 사용하는 port입니다.
using System;
using System.Threading;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface ICheckpointRuntime
    {
        MatchConfig Config { get; }
        IMatchTime Time { get; }
        bool IsCurrent { get; }
        bool IsAuthority { get; }
        bool CanCapture { get; }
        bool CanRestore { get; }
        CancellationToken Lifetime { get; }
        IDisposable AcquirePause(string owner);
        /// <summary>이전 요청/예약을 닫고 게임 초·epoch를 바꿉니다. 실패 복구에서도 백업 시각으로 다시 호출합니다.</summary>
        void BeginRestore(double gameSeconds);
        /// <summary>모든 적용 또는 모든 백업 복구 뒤 실제 공간/환경 읽기 자료를 갱신합니다.</summary>
        void FinishRestore();
        /// <summary>백업 복구/정리 실패 시 부분 상태가 재개되지 않도록 소유 Match를 닫고 오류를 보고합니다. 승패·보상을 생성하지 않습니다.</summary>
        void Fault(Exception error);
    }
}

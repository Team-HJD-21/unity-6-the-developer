// 게임 규칙 소유자가 주입할 이벤트 원본입니다. 이번 단계에서 특정 게임 상황은 연결하지 않습니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface ICheckpointRequestSource
    {
        /// <summary>소유 메인 스레드에서 발행합니다. Sequence는 같은 원본/관찰자 수명 동안 증가하며 오래된 요청을 재실행하지 않습니다.</summary>
        event Action<CheckpointRequest> Requested;
    }
}

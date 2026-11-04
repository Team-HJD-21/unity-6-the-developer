// 호출자가 완료 결과와 현재 Scope 여부를 확인할 수 있는 실행 결과입니다. 영속 저장 자료는 아닙니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class CheckpointOperationResult
    {
        public Guid OperationId { get; }
        public CheckpointOperationStatus Status { get; }
        public CheckpointData Data { get; }
        public string Reason { get; }
        public Exception Error { get; }
        public bool IsCurrentScope { get; }
        public bool IsSuccess => Status == CheckpointOperationStatus.Saved || Status == CheckpointOperationStatus.Restored;
        public CheckpointOperationResult(Guid operationId, CheckpointOperationStatus status, string reason,
            bool isCurrentScope, CheckpointData data = null, Exception error = null)
        { OperationId = operationId; Status = status; Reason = reason ?? ""; IsCurrentScope = isCurrentScope; Data = data; Error = error; }
    }
}

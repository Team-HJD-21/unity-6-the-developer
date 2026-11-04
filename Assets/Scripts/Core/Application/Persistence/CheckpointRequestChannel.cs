// 명시 호출을 이벤트로 전달하는 기본 원본입니다. 폴링이나 자동 저장 조건을 갖지 않습니다.
using System;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Application
{
    public sealed class CheckpointRequestChannel : ICheckpointRequestSource
    {
        private readonly int _thread = Environment.CurrentManagedThreadId;
        public event Action<CheckpointRequest> Requested;
        public void Publish(CheckpointRequest request)
        {
            if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Publish checkpoint intents on the owning thread.");
            if (request == null) throw new ArgumentNullException(nameof(request));
            Requested?.Invoke(request);
        }
    }
}

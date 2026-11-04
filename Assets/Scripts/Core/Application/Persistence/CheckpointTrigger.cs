// 주입된 이벤트만 관찰해 실행 서비스에 전달하고 Scope·중복·과거 요청을 통제합니다.
// 자동 발동 상황을 선택하거나 Feature 상태를 직접 읽고 쓰지 않습니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Application
{
    public sealed class CheckpointTrigger : IDisposable
    {
        private readonly ICheckpointRequestSource _source;
        private readonly CheckpointService _service;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly int _thread = Environment.CurrentManagedThreadId;
        private CheckpointRequest _latest;
        private CheckpointOperationResult _latestResult;
        private int _pending;
        private bool _disposed;
        public int PendingRequests => Volatile.Read(ref _pending);
        public Exception LastObserverError { get; private set; }
        public event Action<CheckpointRequest, CheckpointOperationResult> Completed;
        public CheckpointTrigger(ICheckpointRequestSource source, CheckpointService service)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source)); _service = service ?? throw new ArgumentNullException(nameof(service));
            _source.Requested += OnRequested;
        }
        private void OnRequested(CheckpointRequest request)
        {
            if (_disposed) return; RequireThread();
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.MatchId != _service.MatchId || request.Epoch != _service.Epoch)
            { Reject(request, "DifferentMatchOrEpoch", current: false); return; }
            if (_latest != null && request.Sequence <= _latest.Sequence)
            {
                if (request.Sequence < _latest.Sequence) { Reject(request, "PastRequest"); return; }
                if (!Same(request, _latest)) { Reject(request, "RequestConflict"); return; }
                if (_latestResult != null) Emit(request, _latestResult);
                return;
            }
            _latest = request; _latestResult = null;
            _ = ExecuteAsync(request);
        }
        private async Task ExecuteAsync(CheckpointRequest request)
        {
            Interlocked.Increment(ref _pending);
            try
            {
                var result = request.Kind == CheckpointRequestKind.Save ? await _service.SaveAsync(request.Slot, _lifetime.Token) :
                    await _service.RestoreAsync(request.Slot, request.Policy, _lifetime.Token);
                if (_disposed) return;
                RequireThread(); if (ReferenceEquals(_latest, request)) _latestResult = result;
                Emit(request, result);
            }
            catch (Exception error)
            {
                LastObserverError = error;
                if (!_disposed && Environment.CurrentManagedThreadId == _thread)
                {
                    var result = new CheckpointOperationResult(Guid.NewGuid(), CheckpointOperationStatus.Failed, error.Message, false, error: error);
                    if (ReferenceEquals(_latest, request)) _latestResult = result;
                    Emit(request, result);
                }
            }
            finally { Interlocked.Decrement(ref _pending); }
        }
        private static bool Same(CheckpointRequest a, CheckpointRequest b) => a.RequestId == b.RequestId && a.MatchId == b.MatchId &&
            a.Epoch == b.Epoch && a.Kind == b.Kind && a.Slot.Equals(b.Slot) && a.Policy.Currency == b.Policy.Currency;
        private void Reject(CheckpointRequest request, string reason, bool current = true)
            => Emit(request, new CheckpointOperationResult(Guid.NewGuid(), CheckpointOperationStatus.Rejected, reason, current));
        private void Emit(CheckpointRequest request, CheckpointOperationResult result)
        {
            if (_disposed || Completed == null) return;
            foreach (Action<CheckpointRequest, CheckpointOperationResult> observer in Completed.GetInvocationList())
                try { observer(request, result); } catch (Exception error) { LastObserverError = error; }
        }
        public void Dispose()
        {
            if (_disposed) return; RequireThread(); _disposed = true; _source.Requested -= OnRequested; Completed = null;
            try { _lifetime.Cancel(); } finally { _lifetime.Dispose(); }
        }
        private void RequireThread() { if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Observe checkpoint requests on the owning thread."); }
    }
}

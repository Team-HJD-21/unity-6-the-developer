// 현재 Match/시간 소유권을 저장 실행기에 연결합니다. Scene Feature 자체는 조립부 callback이 관리합니다.
using System;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class MatchCheckpointRuntime : ICheckpointRuntime
    {
        private readonly MatchSession _session;
        private readonly MatchTimeService _time;
        private readonly Func<bool> _current, _authority, _canCapture;
        private readonly Action _beforeRestore, _afterRestore;
        private readonly Action<Exception> _fault;
        public MatchConfig Config => _session.Config;
        public IMatchTime Time => _time.Clock;
        public bool IsCurrent => !_session.IsDisposed && _current();
        public bool IsAuthority => IsCurrent && _authority();
        public bool CanCapture => IsAuthority && _session.IsGameplayRunning && Time.IsAvailable && _canCapture();
        public bool CanRestore => IsAuthority && (_session.IsGameplayRunning || _session.State.Phase == MatchPhase.Preparing) && Time.IsAvailable;
        public CancellationToken Lifetime { get; }
        public MatchCheckpointRuntime(MatchSession session, MatchTimeService time, Func<bool> current, Func<bool> authority,
            CancellationToken lifetime, Action beforeRestore, Action afterRestore, Action<Exception> fault, Func<bool> canCapture = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session)); _time = time ?? throw new ArgumentNullException(nameof(time));
            _current = current ?? throw new ArgumentNullException(nameof(current)); _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            _canCapture = canCapture ?? (() => true);
            Lifetime = lifetime; _beforeRestore = beforeRestore ?? throw new ArgumentNullException(nameof(beforeRestore));
            _afterRestore = afterRestore ?? throw new ArgumentNullException(nameof(afterRestore)); _fault = fault ?? throw new ArgumentNullException(nameof(fault));
        }
        public IDisposable AcquirePause(string owner) => _time.AcquirePause(owner);
        public void BeginRestore(double gameSeconds) { _beforeRestore(); _time.Restore(gameSeconds); }
        public void FinishRestore() => _afterRestore();
        public void Fault(Exception error) => _fault(error);
    }
}

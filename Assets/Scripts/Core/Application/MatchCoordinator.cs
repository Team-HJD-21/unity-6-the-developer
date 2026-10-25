// 실제 Feature 사실을 모드 정책에 전달하고 한 Match의 단일 완료를 조율합니다.
// 완료/폐기 때 gameplay 취소 토큰과 구독을 닫으며 Session 자체는 소유하지 않습니다.
// 게임 시간은 Unity adapter가 전달하고 네트워크 연결·UI·HP 원본은 별도 소유자가 관리합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchCoordinator : IDisposable, IMatchClock
    {
        private readonly MatchSession _session;
        private readonly IControlUnitHealthSource _controlUnit;
        private readonly Func<bool> _isAuthority;
        private readonly CancellationTokenSource _gameplay = new CancellationTokenSource();
        private readonly CancellationToken _gameplayToken;
        private IDisposable _events;
        private bool _started, _disposed, _evaluating, _completionObserved;
        private long _sequence;
        public MatchId MatchId => _session.Config.MatchId;
        public MatchCompletion Completion => _session.Completion;
        public double ElapsedSeconds { get; private set; }
        public CancellationToken GameplayCancellation => _gameplayToken;
        public bool IsGameplayRunning => _started && !_disposed && !_gameplayToken.IsCancellationRequested &&
            _session.IsGameplayRunning && _isAuthority();
        public event Action<MatchCompletion> Completed;

        public MatchCoordinator(MatchSession session, IControlUnitHealthSource controlUnit, Func<bool> isAuthority)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _controlUnit = controlUnit ?? throw new ArgumentNullException(nameof(controlUnit));
            _isAuthority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
            _gameplayToken = _gameplay.Token;
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MatchCoordinator));
            if (_started) throw new InvalidOperationException("The Match coordinator is already started.");
            if (!_session.IsGameplayRunning || !_isAuthority()) throw new InvalidOperationException("Start with the current authoritative running Match.");
            _started = true;
            _controlUnit.Destroyed += EvaluateProgress;
            _session.Disposing += Dispose;
            _events = _session.Events.Subscribe(OnMatchEvent);
            EvaluateProgress();
        }

        public void Tick(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!IsGameplayRunning) return;
            double next = ElapsedSeconds + deltaSeconds;
            if (double.IsInfinity(next)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            ElapsedSeconds = next;
        }

        public void EvaluateProgress()
        {
            if (!IsGameplayRunning || _evaluating) return;
            _evaluating = true;
            try
            {
                var progress = new MatchProgressSnapshot(MatchId, _controlUnit.CurrentHealth,
                    _controlUnit.MaxHealth, ElapsedSeconds);
                MatchOutcome? outcome = _session.EvaluateProgress(progress);
                if (outcome.HasValue) _session.Complete(outcome.Value, checked(++_sequence));
            }
            finally { _evaluating = false; }
        }

        private void OnMatchEvent(MatchEvent fact)
        {
            if (_disposed || !_started) return;
            if (!(fact is MatchCompleted completed) || completed.MatchId != MatchId || _completionObserved) return;
            _completionObserved = true;
            var errors = new List<Exception>();
            TryCleanup(_gameplay.Cancel, errors);
            Action<MatchCompletion> handlers = Completed;
            if (handlers != null)
                foreach (Action<MatchCompletion> handler in handlers.GetInvocationList())
                    TryCleanup(() => handler(completed.Completion), errors);
            if (errors.Count > 0) throw new AggregateException("Match completed; cancellation or observers failed.", errors);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _controlUnit.Destroyed -= EvaluateProgress;
            _session.Disposing -= Dispose;
            _events?.Dispose(); _events = null;
            Completed = null;
            var errors = new List<Exception>();
            TryCleanup(_gameplay.Cancel, errors);
            TryCleanup(_gameplay.Dispose, errors);
            if (errors.Count > 0) throw new AggregateException("Match coordinator cleanup failed.", errors);
        }

        private static void TryCleanup(Action action, List<Exception> errors)
        {
            try { action(); } catch (Exception error) { errors.Add(error); }
        }
    }
}

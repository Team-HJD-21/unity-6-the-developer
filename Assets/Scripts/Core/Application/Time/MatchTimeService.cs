// Scene 조립부가 한 권위 Match의 시계·예약과 종료 수명을 함께 소유합니다.
// 완료/폐기 시작에는 예약을 즉시 차단하고 소비자 해제 뒤 서비스 자체를 Dispose합니다.
// Clock은 읽기 경계로 제공하고 시간 제어는 현재 Match의 권위 소유자만 수행합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchTimeService : IDisposable
    {
        private readonly MatchSession _session;
        private readonly Func<bool> _isAuthority;
        private readonly MatchClock _clock;
        private readonly MatchScheduler _scheduler;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private IDisposable _events;
        private readonly int _ownerThread = System.Environment.CurrentManagedThreadId;
        private readonly HashSet<PauseLease> _pauses = new HashSet<PauseLease>();
        private bool _policyPaused;
        private bool _stopped, _disposed;
        public IMatchTime Clock => _clock;
        public IMatchScheduler Scheduler => _scheduler;
        public int PendingTimers => _stopped || _disposed ? 0 : _scheduler.PendingCount;
        public bool IsGameplayRunning => !_stopped && !_disposed && _session.IsGameplayRunning && _isAuthority();
        /// <summary>진단용 현재 임시 정지 소유자 사본입니다. 참가자 정책 정지와 구분합니다.</summary>
        public IReadOnlyList<string> PauseOwners
        { get { var owners = new List<string>(); foreach (var lease in _pauses) owners.Add(lease.Owner); return owners.AsReadOnly(); } }

        public MatchTimeService(MatchSession session, ITimestampSource source, Func<bool> isAuthority)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _isAuthority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
            try
            {
                _clock = new MatchClock(session.Config.MatchId, source);
                if (!_clock.IsAvailable) throw new InvalidOperationException("The Match timestamp source must be ready before composition.");
                if (session.State.Phase == MatchPhase.Preparing) _clock.SetPaused(true);
                _scheduler = new MatchScheduler(_clock, lifetime: _lifetime.Token);
                _events = session.Events.Subscribe(fact => { if (fact is MatchCompleted) Stop(); });
                session.Disposing += Stop;
            }
            catch (Exception initializationError)
            {
                var errors = new List<Exception>();
                Cleanup(() => _events?.Dispose(), errors); Cleanup(() => _scheduler?.Dispose(), errors);
                Cleanup(() => _clock?.Dispose(), errors); Cleanup(_lifetime.Dispose, errors);
                if (errors.Count > 0) { errors.Insert(0, initializationError); throw new AggregateException(errors); }
                throw;
            }
        }

        /// <summary>Scene 조립부가 매 프레임 현재 권위 게임 배속을 전달합니다. SDK 시간 원본은 계속 진행합니다.</summary>
        public void Pump(double rate)
        {
            RequireOwnerThread();
            if (_disposed) throw new ObjectDisposedException(nameof(MatchTimeService));
            if (!_stopped && !_session.IsDisposed && _session.State.Phase == MatchPhase.Preparing && _isAuthority())
            { ApplyPause(); _clock.Pump(); return; }
            if (!IsGameplayRunning) { Stop(); return; }
            ApplyPause();
            if (_clock.Snapshot.Rate != rate) _clock.SetRate(rate);
            _clock.Pump(); _scheduler.Pump();
        }

        /// <summary>권위의 필드 정지 정책이 호출합니다. 개인 메뉴/Client의 로컬 시간 변경은 이 경계로 보내지 않습니다.</summary>
        public void SetPaused(bool paused)
        { RequireAuthority(); _policyPaused = paused; ApplyPause(); }
        /// <summary>캡처·복원 조율부가 메인 스레드에서 임시 정지를 임대합니다. 해제해도 다른 소유자/우주선 정지는 유지합니다.</summary>
        /// <remarks>준비 중 복원도 허용합니다. 반환 lease는 같은 소유 스레드에서 Dispose하며 완료/폐기 후 해제는 재개하지 않습니다.</remarks>
        public IDisposable AcquirePause(string owner)
        {
            RequireAuthority(allowPreparing: true);
            if (string.IsNullOrWhiteSpace(owner) || owner.Length > 128) throw new ArgumentException("A bounded pause owner is required.", nameof(owner));
            var lease = new PauseLease(this, owner); _pauses.Add(lease);
            try { ApplyPause(); return lease; }
            catch { _pauses.Remove(lease); throw; }
        }
        private void ApplyPause()
        {
            bool paused = _policyPaused || _pauses.Count > 0 || _stopped || _session.State.Phase == MatchPhase.Preparing;
            if (_clock.Snapshot.IsPaused != paused) _clock.SetPaused(paused);
        }
        private void ReleasePause(PauseLease lease)
        {
            if (_disposed || _stopped) return;
            RequireOwnerThread();
            if (_pauses.Remove(lease) && _isAuthority() && !_session.IsDisposed) ApplyPause();
        }
        private sealed class PauseLease : IDisposable
        {
            private MatchTimeService _service;
            public string Owner { get; }
            public PauseLease(MatchTimeService service, string owner) { _service = service; Owner = owner; }
            public void Dispose()
            { var service = _service; if (service == null) return; service.ReleasePause(this); _service = null; }
        }
        /// <summary>저장 복원 조율부가 현재 Match의 게임 초를 복원하고 과거 예약을 취소합니다.</summary>
        public void Restore(double gameSeconds)
        { RequireAuthority(allowPreparing: true); _clock.Restore(gameSeconds); _scheduler.CancelPending(); ApplyPause(); }

        private void Stop()
        {
            if (_stopped || _disposed) return;
            RequireOwnerThread();
            _stopped = true;
            _pauses.Clear();
            var errors = new List<Exception>();
            Cleanup(_lifetime.Cancel, errors); Cleanup(_scheduler.CancelPending, errors);
            Cleanup(() => _clock.SetPaused(true), errors);
            if (errors.Count > 0) throw new AggregateException("Match time stopped; cleanup errors occurred.", errors);
        }
        private void RequireOwnerThread()
        { if (System.Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("Control Match time on its owning thread."); }
        private void RequireAuthority(bool allowPreparing = false)
        {
            RequireOwnerThread();
            if (_disposed) throw new ObjectDisposedException(nameof(MatchTimeService));
            if (!IsGameplayRunning && !(allowPreparing && !_stopped && !_session.IsDisposed &&
                _session.State.Phase == MatchPhase.Preparing && _isAuthority()))
                throw new InvalidOperationException("Control time only for the current authoritative active Match.");
        }
        /// <summary>소비자의 예약/바인딩 해제 뒤 Scene 소유자가 시계와 예약 자원을 폐기합니다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            RequireOwnerThread();
            _session.Disposing -= Stop;
            var errors = new List<Exception>();
            Cleanup(Stop, errors); _disposed = true;
            Cleanup(() => _events?.Dispose(), errors); _events = null;
            Cleanup(_scheduler.Dispose, errors); Cleanup(_clock.Dispose, errors); Cleanup(_lifetime.Dispose, errors);
            if (errors.Count > 0) throw new AggregateException("Match time disposal failed after all resources were released.", errors);
        }
        private static void Cleanup(Action cleanup, List<Exception> errors)
        { try { cleanup(); } catch (Exception error) { errors.Add(error); } }
    }
}

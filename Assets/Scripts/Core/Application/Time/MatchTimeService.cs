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
        private bool _stopped, _disposed;
        public IMatchTime Clock => _clock;
        public IMatchScheduler Scheduler => _scheduler;
        public int PendingTimers => _stopped || _disposed ? 0 : _scheduler.PendingCount;
        public bool IsGameplayRunning => !_stopped && !_disposed && _session.IsGameplayRunning && _isAuthority();

        public MatchTimeService(MatchSession session, ITimestampSource source, Func<bool> isAuthority)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _isAuthority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
            try
            {
                _clock = new MatchClock(session.Config.MatchId, source);
                if (!_clock.IsAvailable) throw new InvalidOperationException("The Match timestamp source must be ready before composition.");
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
            if (_disposed) throw new ObjectDisposedException(nameof(MatchTimeService));
            if (!IsGameplayRunning) { Stop(); return; }
            if (_clock.Snapshot.Rate != rate) _clock.SetRate(rate);
            _clock.Pump(); _scheduler.Pump();
        }

        /// <summary>권위의 필드 정지 정책이 호출합니다. 개인 메뉴/Client의 로컬 시간 변경은 이 경계로 보내지 않습니다.</summary>
        public void SetPaused(bool paused) { RequireAuthority(); _clock.SetPaused(paused); }
        /// <summary>저장 복원 조율부가 현재 Match의 게임 초를 복원하고 과거 예약을 취소합니다.</summary>
        public void Restore(double gameSeconds)
        { RequireAuthority(); _clock.Restore(gameSeconds); _scheduler.CancelPending(); }

        private void Stop()
        {
            if (_stopped || _disposed) return;
            _stopped = true;
            var errors = new List<Exception>();
            Cleanup(_lifetime.Cancel, errors); Cleanup(_scheduler.CancelPending, errors);
            Cleanup(() => _clock.SetPaused(true), errors);
            if (errors.Count > 0) throw new AggregateException("Match time stopped; cleanup errors occurred.", errors);
        }
        private void RequireAuthority()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MatchTimeService));
            if (!IsGameplayRunning) throw new InvalidOperationException("Control time only for the current authoritative running Match.");
        }
        /// <summary>소비자의 예약/바인딩 해제 뒤 Scene 소유자가 시계와 예약 자원을 폐기합니다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
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

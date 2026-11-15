// MatchCoordinator가 소유하는 정규 생산 예약입니다. Scene/Debugger가 별도 자동 루프를 만들지 않습니다.
// 기존 Match Scheduler의 단발 예약을 이어 사용해 정지·긴 프레임 뒤에도 밀린 요청을 몰아내지 않습니다.
// 편성·예산·생성은 공통 요청 서비스에 맡기며 이 클래스는 게임 시각과 요청 결과만 보관합니다.
using System;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchEncounterProduction : IDisposable
    {
        private readonly MatchId _match;
        private readonly PlayerId _issuer;
        private readonly IEncounterRequestService _requests;
        private readonly IMatchScheduler _scheduler;
        private readonly IMatchClock _clock;
        private readonly Func<bool> _canProduce;
        private readonly CancellationToken _lifetime;
        private readonly EncounterProductionSettings _settings;
        private IDisposable _pending;
        private bool _started, _disposed;
        private double _due;
        public long AttemptedRequests { get; private set; }
        public EncounterRequestResult LastResult { get; private set; }
        public bool IsRunning => _started && !_disposed && !_lifetime.IsCancellationRequested && _settings.Enabled;
        public double RemainingSeconds => IsRunning ? Math.Max(0, _due - _clock.ElapsedSeconds) : 0;
        public double IntervalSeconds => _settings.IntervalSeconds;

        /// <summary>조립부가 권위 Match의 서비스·시계·설정을 전달합니다. 생성만으로 예약하지 않습니다.</summary>
        public MatchEncounterProduction(MatchId match, PlayerId issuer, IEncounterRequestService requests,
            IMatchScheduler scheduler, IMatchClock clock, Func<bool> canProduce,
            EncounterProductionSettings settings, CancellationToken lifetime = default)
        {
            if (match.IsEmpty || issuer.IsEmpty) throw new ArgumentException("A Match and verified authority participant are required.");
            if (settings == null || !settings.IsValid) throw new ArgumentException("Invalid Encounter production settings.", nameof(settings));
            _match = match; _issuer = issuer;
            _requests = requests ?? throw new ArgumentNullException(nameof(requests));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _canProduce = canProduce ?? throw new ArgumentNullException(nameof(canProduce));
            _settings = settings.Copy(); _lifetime = lifetime;
        }

        /// <summary>Coordinator가 Match 시작 뒤 한 번 호출합니다. Client는 이 소유자를 생성하지 않습니다.</summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MatchEncounterProduction));
            if (_started) throw new InvalidOperationException("Production already started.");
            _started = true;
            if (IsRunning) Schedule(_settings.FirstDelaySeconds);
        }

        private void Schedule(double delay)
        {
            _due = _clock.ElapsedSeconds + delay;
            _pending = _scheduler.Schedule(delay, Produce, _lifetime);
        }

        private void Produce(MatchTimerTick tick)
        {
            _pending = null;
            if (!IsRunning) return;
            try
            {
                if (!_canProduce()) return;
                var request = EncounterRequest.Automatic(
                    new MatchRequestContext(_match, Guid.NewGuid(), _issuer),
                    _settings.MaximumEnemiesPerRequest, EncounterRequestSource.MatchProduction);
                AttemptedRequests = checked(AttemptedRequests + 1);
                LastResult = _requests.Submit(request, _issuer);
            }
            finally
            {
                // 현재 실행 시각부터 다음 회차를 잡습니다. 과거에 놓친 회차를 한 프레임에 따라잡지 않습니다.
                if (IsRunning) Schedule(_settings.IntervalSeconds);
            }
        }

        /// <summary>저장 조립부가 남은 게임 초와 요청 횟수를 읽습니다. 진단 결과는 월드 원본이 아닙니다.</summary>
        public State Capture() => new State(RemainingSeconds, AttemptedRequests);

        /// <summary>시계/epoch 복원 뒤 호출합니다. 과거 예약을 취소하고 남은 첫 대기만 새 세대에 등록합니다.</summary>
        public void Restore(State state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_disposed || !_started) throw new InvalidOperationException("Restore active production only.");
            _pending?.Dispose(); _pending = null;
            AttemptedRequests = state.AttemptedRequests; LastResult = null;
            if (IsRunning) Schedule(state.RemainingSeconds);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _pending?.Dispose(); _pending = null;
        }

        public sealed class State
        {
            public double RemainingSeconds { get; }
            public long AttemptedRequests { get; }
            public State(double remainingSeconds, long attemptedRequests)
            {
                if (!double.IsFinite(remainingSeconds) || remainingSeconds < 0 || attemptedRequests < 0)
                    throw new ArgumentOutOfRangeException(nameof(remainingSeconds));
                RemainingSeconds = remainingSeconds; AttemptedRequests = attemptedRequests;
            }
        }
    }
}

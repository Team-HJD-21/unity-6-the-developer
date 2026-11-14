// 실제 Feature 사실을 모드 정책에 전달하고 한 Match의 단일 완료를 조율합니다.
// 완료/폐기 때 gameplay 취소 토큰과 구독을 닫으며 Session 자체는 소유하지 않습니다.
// 정식 게임 시간은 주입된 공용 시계에서 읽고 네트워크 연결·UI·HP 원본은 별도 소유자가 관리합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchCoordinator : IDisposable, IMatchClock
    {
        private readonly MatchSession _session;
        private readonly IControlUnitVitals _controlUnit;
        private readonly IControlUnitHealthSource _destructionEvents;
        private readonly Func<bool> _isAuthority;
        private readonly IMatchClock _clock;
        private double _manualElapsed;
        private readonly CancellationTokenSource _gameplay = new CancellationTokenSource();
        private readonly CancellationToken _gameplayToken;
        private IDisposable _events;
        private bool _started, _disposed, _evaluating, _completionObserved;
        private long _sequence;
        public MatchRecovery Recovery { get; private set; }
        public StageProgression StageProgression { get; private set; }
        public MatchEncounterProduction EncounterProduction { get; private set; }
        public MatchId MatchId => _session.Config.MatchId;
        public MatchCompletion Completion => _session.Completion;
        public double ElapsedSeconds => _clock?.ElapsedSeconds ?? _manualElapsed;
        public CancellationToken GameplayCancellation => _gameplayToken;
        public bool IsGameplayRunning => _started && !_disposed && !_gameplayToken.IsCancellationRequested &&
            _session.IsGameplayRunning && _isAuthority();
        public event Action<MatchCompletion> Completed;

        /// <summary>Core 조립부가 실제 CU의 읽기 원본을 연결합니다. 파괴 알림 Port가 있으면 함께 구독합니다.</summary>
        /// <remarks>기존 CU는 IControlUnitVitals 그대로 사용하며 Scene이 매 프레임 EvaluateProgress를 호출합니다.</remarks>
        public MatchCoordinator(MatchSession session, IControlUnitVitals controlUnit, Func<bool> isAuthority,
            IMatchClock clock = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _controlUnit = controlUnit ?? throw new ArgumentNullException(nameof(controlUnit));
            _destructionEvents = controlUnit as IControlUnitHealthSource;
            _isAuthority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
            _clock = clock;
            _gameplayToken = _gameplay.Token;
        }

        /// <summary>Scene 조립부가 공통 요청 서비스를 연결합니다. 시작 뒤에는 Coordinator만 정규 생산을 소유합니다.</summary>
        public void ConfigureEncounterProduction(PlayerId issuer, IEncounterRequestService requests,
            IMatchScheduler scheduler, IMatchClock productionClock, EncounterProductionSettings settings, Func<bool> canProduce)
        {
            if (_started || _disposed || EncounterProduction != null || _clock == null)
                throw new InvalidOperationException("Configure one production owner before Match start.");
            EncounterProduction = new MatchEncounterProduction(MatchId, issuer, requests, scheduler, productionClock,
                () => IsGameplayRunning && canProduce(), settings, _gameplayToken);
        }

        /// <summary>Scene이 시작 전에 실제 roster/공용 시각에 생존 조율부를 붙입니다.</summary>
        public void ConfigureRecovery(MissionRecoveryLedger ledger, MissionRecoverySettings settings)
        {
            if (_started || _disposed || Recovery != null || _clock == null)
                throw new InvalidOperationException("Configure one recovery owner before Match start.");
            Recovery = new MatchRecovery(ledger, _clock, () => IsGameplayRunning,
                _session.Config.Players.Select(player => player.PlayerId), settings);
        }

        /// <summary>Scene이 영속 실행 ID를 읽은 뒤 실제 Field 사건 원본을 연결합니다. 콘텐츠 미연결은 Snapshot에 남깁니다.</summary>
        public void ConfigureStageProgression(Guid run, IMatchClock fieldClock, IFieldBossSpawnService spawns)
        {
            if (_disposed || StageProgression != null) throw new InvalidOperationException("Configure Stage progression once.");
            StageProgression = new StageProgression(MatchId, run, fieldClock,
                () => IsGameplayRunning && Recovery?.IsRecoveryPending != true, spawns);
            // 진행/사망 사실만 제공합니다. 최종 승리 정책이 확정되기 전에는 자동으로 Match를 완료하지 않습니다.
        }

        /// <summary>모드/Stage 소유자가 확정한 승패를 한 번만 완료합니다. 비환급 기록은 쓰기 권한이 닫히기 전에 확정합니다.</summary>
        public void CompleteMatch(MatchOutcome outcome, MatchEndReason reason = MatchEndReason.None)
        {
            if (!IsGameplayRunning) throw new InvalidOperationException("Complete only the current authority Match.");
            Recovery?.Ledger.EndMission();
            _session.Complete(outcome, checked(++_sequence), reason);
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MatchCoordinator));
            if (_started) throw new InvalidOperationException("The Match coordinator is already started.");
            if (!_session.IsGameplayRunning || !_isAuthority()) throw new InvalidOperationException("Start with the current authoritative running Match.");
            _started = true;
            if (_destructionEvents != null)
                _destructionEvents.Destroyed += EvaluateProgress;
            _session.Disposing += Dispose;
            _events = _session.Events.Subscribe(OnMatchEvent);
            EvaluateProgress();
            if (IsGameplayRunning) EncounterProduction?.Start();
        }

        /// <summary>공용 시계를 주입하지 않은 독립 검사/호환 실행에서만 게임 초를 수동 진행합니다.</summary>
        /// <remarks>정식 Scene은 MatchTimeService를 Pump합니다. 주입된 시계와 이 누적 경로를 동시에 사용하지 않습니다.</remarks>
        public void Tick(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (_clock != null) throw new InvalidOperationException("Pump the injected Match time service instead of accumulating a second clock.");
            if (!IsGameplayRunning) return;
            double next = ElapsedSeconds + deltaSeconds;
            if (double.IsInfinity(next)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            _manualElapsed = next;
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
                if (outcome.HasValue) CompleteMatch(outcome.Value, outcome == MatchOutcome.Defeat && _controlUnit.CurrentHealth == 0
                    ? MatchEndReason.ControlUnitDestroyed : MatchEndReason.None);
            }
            finally { _evaluating = false; }
        }

        private void OnMatchEvent(MatchEvent fact)
        {
            if (_disposed || !_started) return;
            if (!(fact is MatchCompleted completed) || completed.MatchId != MatchId || _completionObserved) return;
            _completionObserved = true;
            var errors = new List<Exception>();
            TryCleanup(() => Recovery?.Dispose(), errors);
            TryCleanup(() => EncounterProduction?.Dispose(), errors);
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
            if (_destructionEvents != null)
                _destructionEvents.Destroyed -= EvaluateProgress;
            _session.Disposing -= Dispose;
            _events?.Dispose(); _events = null;
            Completed = null;
            var errors = new List<Exception>();
            TryCleanup(() => Recovery?.Dispose(), errors);
            TryCleanup(() => EncounterProduction?.Dispose(), errors);
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

// Match 하나의 수명과 명령 흐름을 관리합니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchSession : IDisposable
    {
        private readonly MatchSimulation _simulation;
        private readonly IModeRules _modeRules;
        private readonly IAuthority _authority;
        private readonly MatchEventBus _eventBus;
        private readonly BattlefieldSpatialRuntime _battlefield;
        private RegionRuntime _regions;
        private MatchEnvironmentRuntime _environment;
        private bool _isDisposed;
        private bool _isCompleting;
        public bool IsDisposed => _isDisposed;
        public MatchCompletion Completion { get; private set; }
        public bool IsGameplayRunning => !_isDisposed && State.Phase == MatchPhase.Running && Completion == null;
        public event Action Disposing;

        public MatchConfig Config { get; }
        public MatchState State { get; }
        public BattlefieldSpatialSnapshot Battlefield => _battlefield.Snapshot;
        public IMatchEventStream Events => _eventBus;
        public IRegionGridSource Regions => _regions;
        public IMatchEnvironmentSource Environment => _environment;

        public IMatchEnvironmentSource InitializeEnvironment(IMatchClock clock, Func<bool> mayWrite)
        {
            ThrowIfDisposed();
            if (_environment != null) throw new InvalidOperationException("Environment is already initialized.");
            _environment = new MatchEnvironmentRuntime(Config.MatchId, clock, mayWrite);
            return _environment;
        }

        public bool UpdateEnvironment(DefinitionId weather, DefinitionId timeOfDay, IEnumerable<DefinitionId> forecast)
        {
            ThrowIfDisposed();
            if (_environment == null) throw new InvalidOperationException("Initialize environment before updating it.");
            return _environment.TryUpdate(weather, timeOfDay, forecast);
        }

        /// <summary>Scene 조립부가 실제 지역 정의를 한 번 연결합니다. RegionRuntime은 Match가 정리합니다.</summary>
        public IRegionGridSource InitializeRegions(RegionGridDefinition definition, RegionPolicyMode mode)
        {
            ThrowIfDisposed();
            if (_regions != null) throw new InvalidOperationException("Regions are already initialized.");
            _regions = new RegionRuntime(Config.MatchId, definition, mode);
            return _regions;
        }

        public RegionUpdateStatus UpdateRegionCapture(RegionGridSnapshot basis, IEnumerable<int> cells,
            RegionCaptureUpdateMode updateMode)
        {
            ThrowIfDisposed();
            if (_regions == null) throw new InvalidOperationException("Regions are not initialized.");
            return _regions.TryApply(basis, cells, updateMode);
        }

        public void ResetRegions(RegionGridDefinition definition, RegionPolicyMode mode, IEnumerable<int> initialCells)
        {
            ThrowIfDisposed();
            if (_regions == null) throw new InvalidOperationException("Regions are not initialized.");
            _regions.Reset(definition, mode, initialCells);
        }

        internal MatchSession(
            MatchConfig config,
            MatchState state,
            MatchSimulation simulation,
            IModeRules modeRules,
            IAuthority authority,
            BattlefieldSpatialInput battlefieldInput,
            BattlefieldDynamicSpatialInput dynamicBattlefieldInput,
            BattlefieldGridConfiguration battlefieldGridConfiguration)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            State = state ?? throw new ArgumentNullException(nameof(state));
            if (Config.MatchId != State.MatchId) throw new ArgumentException("Match config and initial state must belong to the same match.", nameof(state));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _modeRules = modeRules ?? throw new ArgumentNullException(nameof(modeRules));
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            _battlefield = new BattlefieldSpatialRuntime(
                State.MatchId,
                battlefieldInput,
                dynamicBattlefieldInput,
                battlefieldGridConfiguration);
            _eventBus = new MatchEventBus();
        }

        public void UpdateBattlefieldParticipants(BattlefieldDynamicSpatialInput dynamicInput)
        {
            ThrowIfDisposed();
            _battlefield.UpdateDynamicInput(dynamicInput);
        }

        public void ReconfigureBattlefieldGrid(BattlefieldGridConfiguration configuration)
        {
            ThrowIfDisposed();
            _battlefield.ReconfigureGrid(configuration);
        }

        public void UpdateBattlefieldTurretLayout(BattlefieldSpatialInput staticInput)
        {
            ThrowIfDisposed();
            _battlefield.UpdateTurretLayout(staticInput);
        }

        public void Start()
        {
            ThrowIfDisposed();
            _simulation.Start(State);
        }

        public CommandSubmissionResult Submit(GameCommand command)
        {
            ThrowIfDisposed();
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (Completion != null)
                return new CommandSubmissionResult(CommandSubmissionStatus.Rejected, "MatchCompleted", null);

            var authorization = _authority.Authorize(command, State);
            if (!authorization.IsAuthorized)
                return new CommandSubmissionResult(CommandSubmissionStatus.Unauthorized, authorization.Reason, null);

            var outcome = _simulation.Execute(State, command, _modeRules);
            foreach (var matchEvent in outcome.Events)
            {
                if (matchEvent.MatchId != State.MatchId)
                    throw new InvalidOperationException("Simulation emitted an event for another match.");
                _eventBus.Publish(matchEvent);
            }
            var status = outcome.Status == SimulationStatus.NotHandled
                ? CommandSubmissionStatus.NotHandled
                : outcome.Status == SimulationStatus.Rejected
                    ? CommandSubmissionStatus.Rejected
                    : CommandSubmissionStatus.Accepted;
            return new CommandSubmissionResult(status, string.Empty, outcome);
        }

        public MatchCompletion Complete(MatchOutcome outcome, long eventSequence)
        {
            ThrowIfDisposed();
            if (eventSequence < 0) throw new ArgumentOutOfRangeException(nameof(eventSequence));
            if (Completion != null)
            {
                if (Completion.Result.Outcome != outcome) throw new InvalidOperationException("A different final outcome is already committed.");
                return Completion;
            }
            if (_isCompleting || State.Phase != MatchPhase.Running)
                throw new InvalidOperationException("Only a running Match can complete, without reentering completion.");
            _isCompleting = true;
            try
            {
                var result = _modeRules.CreateResult(State, outcome);
                if (result == null) throw new InvalidOperationException("Mode rules must provide a match result.");
                if (result.MatchId != State.MatchId) throw new InvalidOperationException("Mode rules returned a result for another match.");
                if (result.Outcome != outcome) throw new InvalidOperationException("Mode rules changed the requested final outcome.");
                var rewardReceipt = _modeRules.CreateRewardReceipt(result);
                if (rewardReceipt == null) throw new InvalidOperationException("Mode rules must provide a reward receipt.");
                var completion = new MatchCompletion(result, rewardReceipt);
                _simulation.Complete(State, result);
                // 관측 실패/재진입에도 동일한 최종 결과를 반환하도록 먼저 보존합니다.
                Completion = completion;
                _eventBus.Publish(new MatchCompleted(State.MatchId, eventSequence, completion));
                return completion;
            }
            finally { _isCompleting = false; }
        }

        public MatchOutcome? EvaluateProgress(MatchProgressSnapshot progress)
        {
            ThrowIfDisposed();
            if (progress == null || progress.MatchId != Config.MatchId)
                throw new ArgumentException("Progress must belong to this Match.", nameof(progress));
            return IsGameplayRunning && _modeRules is IMatchProgressRules rules ? rules.Evaluate(progress) : null;
        }

        public MatchSnapshot CreateSnapshot(long sequence)
        {
            ThrowIfDisposed();
            return State.CreateSnapshot(sequence);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            var errors = new List<Exception>();
            Action handlers = Disposing;
            Disposing = null;
            if (handlers != null)
            {
                // Feature가 먼저 연결을 해제한 뒤 Match 소유 자료를 폐기합니다.
                foreach (Action handler in handlers.GetInvocationList())
                    try { handler(); }
                    catch (Exception exception) { errors.Add(exception); }
            }
            DisposeOwned(() => _simulation.Dispose(State), errors);
            DisposeOwned(_eventBus.Dispose, errors);
            DisposeOwned(_battlefield.Dispose, errors);
            if (_environment != null) DisposeOwned(_environment.Dispose, errors);
            if (_regions != null) DisposeOwned(_regions.Dispose, errors);
            if (errors.Count > 0) throw new AggregateException("Match cleanup failed after releasing all owned resources.", errors);
        }

        private static void DisposeOwned(Action cleanup, List<Exception> errors)
        {
            try { cleanup(); }
            catch (Exception exception) { errors.Add(exception); }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MatchSession));
        }
    }
}

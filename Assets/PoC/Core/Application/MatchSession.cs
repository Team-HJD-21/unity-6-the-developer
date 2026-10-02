// Match 하나의 수명과 명령 흐름을 관리합니다.

using System;
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
        private bool _isDisposed;

        public MatchConfig Config { get; }
        public MatchState State { get; }
        public BattlefieldSpatialSnapshot Battlefield => _battlefield.Snapshot;
        public IMatchEventStream Events => _eventBus;

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
            var result = _modeRules.CreateResult(State, outcome);
            if (result == null) throw new InvalidOperationException("Mode rules must provide a match result.");
            if (result.MatchId != State.MatchId) throw new InvalidOperationException("Mode rules returned a result for another match.");
            var rewardReceipt = _modeRules.CreateRewardReceipt(result);
            if (rewardReceipt == null) throw new InvalidOperationException("Mode rules must provide a reward receipt.");
            var completion = new MatchCompletion(result, rewardReceipt);
            _simulation.Complete(State, result);
            _eventBus.Publish(new MatchCompleted(State.MatchId, eventSequence, completion));
            return completion;
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
            Exception simulationError = null;
            try
            {
                _simulation.Dispose(State);
            }
            catch (Exception exception)
            {
                simulationError = exception;
            }
            finally
            {
                _eventBus.Dispose();
                _battlefield.Dispose();
            }
            if (simulationError != null) throw new InvalidOperationException("Match simulation failed to dispose.", simulationError);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MatchSession));
        }
    }
}

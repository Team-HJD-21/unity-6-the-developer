// Owns the lifetime and command flow for exactly one match.

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
        private bool _isDisposed;

        public MatchConfig Config { get; }
        public MatchState State { get; }
        public IMatchEventStream Events => _eventBus;

        internal MatchSession(MatchConfig config, MatchState state, MatchSimulation simulation, IModeRules modeRules, IAuthority authority)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            State = state ?? throw new ArgumentNullException(nameof(state));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _modeRules = modeRules ?? throw new ArgumentNullException(nameof(modeRules));
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            _eventBus = new MatchEventBus();
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
            foreach (var matchEvent in outcome.Events) _eventBus.Publish(matchEvent);
            var status = outcome.Status == SimulationStatus.NotHandled
                ? CommandSubmissionStatus.NotHandled
                : outcome.Status == SimulationStatus.Rejected
                    ? CommandSubmissionStatus.Rejected
                    : CommandSubmissionStatus.Accepted;
            return new CommandSubmissionResult(status, string.Empty, outcome);
        }

        public MatchResult Complete(MatchOutcome outcome, long eventSequence)
        {
            ThrowIfDisposed();
            var result = _modeRules.CreateResult(State, outcome);
            if (result == null) throw new InvalidOperationException("Mode rules must provide a match result.");
            _simulation.Complete(State, result);
            _eventBus.Publish(new MatchCompleted(State.MatchId, eventSequence, result));
            return result;
        }

        public MatchSnapshot CreateSnapshot(long sequence)
        {
            ThrowIfDisposed();
            return State.CreateSnapshot(sequence);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _simulation.Dispose(State);
            _eventBus.Dispose();
            _isDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MatchSession));
        }
    }
}

// Persistent Unity owner for app-scoped service composition and shutdown.

using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour
    {
        private MatchSession _currentMatch;
        private bool _isDisposed;

        internal AppServices Services { get; private set; }

        public void Initialize(AppServices services)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppRoot));
            if (Services != null) throw new InvalidOperationException("AppRoot has already been initialized.");
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public MatchSession StartMatch(MatchConfig config, MatchState initialState, IModeRules modeRules)
        {
            ThrowIfUnavailable();
            var nextMatch = Services.MatchSessions.Create(config, initialState, modeRules);
            try
            {
                nextMatch.Start();
            }
            catch
            {
                nextMatch.Dispose();
                throw;
            }

            EndCurrentMatch();
            _currentMatch = nextMatch;
            return nextMatch;
        }

        public MatchCompletion CompleteCurrentMatch(MatchOutcome outcome, long eventSequence)
        {
            ThrowIfUnavailable();
            if (_currentMatch == null) throw new InvalidOperationException("There is no active match.");
            return _currentMatch.Complete(outcome, eventSequence);
        }

        public void EndCurrentMatch()
        {
            _currentMatch?.Dispose();
            _currentMatch = null;
        }

        private void OnDestroy()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            EndCurrentMatch();
            Services?.Dispose();
            Services = null;
        }

        private void ThrowIfUnavailable()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppRoot));
            if (Services == null) throw new InvalidOperationException("AppRoot has not been initialized.");
        }
    }
}

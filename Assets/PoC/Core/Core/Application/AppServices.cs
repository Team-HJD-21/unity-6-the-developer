// Owns app-scoped services and the currently active match session.

using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class AppServices : IDisposable
    {
        private bool _isDisposed;

        public IProfileService Profiles { get; }
        public IContentCatalog Content { get; }
        public IPlatformService Platform { get; }
        public ISceneFlow SceneFlow { get; }
        public MatchSessionFactory MatchSessions { get; }
        public MatchSession CurrentMatch { get; private set; }

        public AppServices(IProfileService profiles, IContentCatalog content, IPlatformService platform, ISceneFlow sceneFlow, IAuthority authority)
        {
            Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            SceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            MatchSessions = new MatchSessionFactory(authority ?? throw new ArgumentNullException(nameof(authority)));
        }

        public MatchSession StartMatch(MatchConfig config, MatchState initialState, IModeRules modeRules)
        {
            ThrowIfDisposed();
            var nextMatch = MatchSessions.Create(config, initialState, modeRules);
            try
            {
                nextMatch.Start();
            }
            catch
            {
                nextMatch.Dispose();
                throw;
            }

            CurrentMatch?.Dispose();
            CurrentMatch = nextMatch;
            return nextMatch;
        }

        public MatchResult CompleteCurrentMatch(MatchOutcome outcome, long eventSequence)
        {
            ThrowIfDisposed();
            if (CurrentMatch == null) throw new InvalidOperationException("There is no active match.");
            return CurrentMatch.Complete(outcome, eventSequence);
        }

        public void EndCurrentMatch()
        {
            if (CurrentMatch == null) return;
            CurrentMatch.Dispose();
            CurrentMatch = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            EndCurrentMatch();
            DisposeIfNeeded(Profiles);
            DisposeIfNeeded(Content);
            DisposeIfNeeded(Platform);
            DisposeIfNeeded(SceneFlow);
            _isDisposed = true;
        }

        private static void DisposeIfNeeded(object service)
        {
            if (service is IDisposable disposable) disposable.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
        }
    }
}

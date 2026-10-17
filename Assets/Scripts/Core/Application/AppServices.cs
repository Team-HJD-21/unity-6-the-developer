// 앱 범위 서비스 의존성과 Match 세션 Factory를 소유합니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;

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
        public INetworkSession NetworkSession { get; private set; }
        public NetworkMatchCoordinator NetworkMatches { get; private set; }
        private IMatchLifecycleTransport _matchTransport;

        public AppServices(IProfileService profiles, IContentCatalog content, IPlatformService platform, ISceneFlow sceneFlow, IAuthority authority,
            INetworkSession networkSession = null)
        {
            Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            SceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            MatchSessions = new MatchSessionFactory(authority ?? throw new ArgumentNullException(nameof(authority)));
            NetworkSession = networkSession;
        }

        /// <summary>조립부가 실제 어댑터의 소유권을 App에 한 번 전달합니다. 연결 시작은 별도 API입니다.</summary>
        public void AttachNetworkSession(INetworkSession session)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (NetworkSession != null && !ReferenceEquals(NetworkSession, session))
                throw new InvalidOperationException("An app network session is already attached.");
            NetworkSession = session;
        }

        /// <summary>성공한 연결의 전송 Port 소유권을 전달합니다. 실제 Match 시작은 별도 명시 API입니다.</summary>
        public NetworkMatchCoordinator AttachNetworkMatchTransport(IMatchLifecycleTransport transport)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (NetworkSession == null) throw new InvalidOperationException("Attach the app network session first.");
            if (NetworkMatches != null) throw new InvalidOperationException("The app match transport is already attached.");
            var matches = new NetworkMatchCoordinator(NetworkSession, transport);
            _matchTransport = transport;
            NetworkMatches = matches;
            return matches;
        }

        public void PumpNetwork()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (_matchTransport is INetworkMessagePump pump) pump.Pump();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            List<Exception> errors = null;
            var matches = NetworkMatches;
            var transport = _matchTransport;
            NetworkMatches = null;
            _matchTransport = null;
            DisposeIfNeeded(matches, ref errors);
            DisposeIfNeeded(transport, ref errors);
            DisposeIfNeeded(Profiles, ref errors);
            DisposeIfNeeded(Content, ref errors);
            DisposeIfNeeded(Platform, ref errors);
            DisposeIfNeeded(SceneFlow, ref errors);
            INetworkSession network = NetworkSession;
            NetworkSession = null;
            DisposeIfNeeded(network, ref errors);
            if (errors != null) throw new AggregateException("One or more app services failed to dispose.", errors);
        }

        private static void DisposeIfNeeded(object service, ref List<Exception> errors)
        {
            if (!(service is IDisposable disposable)) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                if (errors == null) errors = new List<Exception>();
                errors.Add(exception);
            }
        }
    }
}

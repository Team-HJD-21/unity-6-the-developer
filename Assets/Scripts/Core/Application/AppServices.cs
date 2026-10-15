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

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            List<Exception> errors = null;
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

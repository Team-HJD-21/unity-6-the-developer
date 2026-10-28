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
        public InputLockService InputLocks { get; }
        public ScreenManager Screens { get; }
        public SceneFlowController SceneTransitions { get; }
        public MatchSessionFactory MatchSessions { get; }
        public INetworkSession NetworkSession { get; private set; }
        public NetworkMatchCoordinator NetworkMatches { get; private set; }
        private IMatchLifecycleTransport _matchTransport;
        public RegionReplicationSession NetworkRegions { get; private set; }
        private IRegionSnapshotTransport _regionTransport;
        public ITurretCommandTransport NetworkTurretCommands { get; private set; }
        public ITurretPresentationTransport NetworkTurretPresentation { get; private set; }

        public AppServices(IProfileService profiles, IContentCatalog content, IPlatformService platform, ISceneFlow sceneFlow, IAuthority authority,
            INetworkSession networkSession = null)
        {
            Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            SceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            InputLocks = new InputLockService();
            Screens = new ScreenManager(InputLocks);
            SceneTransitions = new SceneFlowController(SceneFlow, InputLocks);
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

        public RegionReplicationSession AttachRegionSnapshotTransport(IRegionSnapshotTransport transport)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (NetworkMatches == null) throw new InvalidOperationException("Attach the Match lifecycle transport first.");
            if (NetworkRegions != null) throw new InvalidOperationException("The region transport is already attached.");
            var regions = new RegionReplicationSession(NetworkSession, NetworkMatches, transport);
            _regionTransport = transport; NetworkRegions = regions;
            return regions;
        }

        public void PumpNetwork()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (_matchTransport is INetworkMessagePump pump) pump.Pump();
            NetworkMatches?.FlushPendingCompletion();
            NetworkRegions?.FlushPending();
            if (_regionTransport is INetworkMessagePump regionPump) regionPump.Pump();
            if (NetworkTurretCommands is INetworkMessagePump turretPump) turretPump.Pump();
            if (NetworkTurretPresentation is INetworkMessagePump presentationPump) presentationPump.Pump();
        }

        /// <summary>접속 수명 동안 전송을 소유합니다. Match별 gateway/정책은 Scene 조립부가 소유합니다.</summary>
        public void AttachTurretCommandTransport(ITurretCommandTransport transport)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (NetworkMatches == null) throw new InvalidOperationException("Attach the Match lifecycle transport first.");
            if (NetworkTurretCommands != null) throw new InvalidOperationException("The turret command transport is already attached.");
            NetworkTurretCommands = transport;
        }

        public void AttachTurretPresentationTransport(ITurretPresentationTransport transport)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (NetworkMatches == null) throw new InvalidOperationException("Attach the Match lifecycle transport first.");
            if (NetworkTurretPresentation != null) throw new InvalidOperationException("The turret presentation transport is already attached.");
            NetworkTurretPresentation = transport;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            List<Exception> errors = null;
            DisposeIfNeeded(SceneTransitions, ref errors);
            DisposeIfNeeded(Screens, ref errors);
            var matches = NetworkMatches;
            var transport = _matchTransport;
            NetworkMatches = null;
            _matchTransport = null;
            DisposeIfNeeded(matches, ref errors);
            var regions = NetworkRegions;
            var regionTransport = _regionTransport;
            NetworkRegions = null; _regionTransport = null;
            DisposeIfNeeded(regions, ref errors);
            DisposeIfNeeded(regionTransport, ref errors);
            var turretTransport = NetworkTurretCommands; NetworkTurretCommands = null;
            DisposeIfNeeded(turretTransport, ref errors);
            var presentationTransport = NetworkTurretPresentation; NetworkTurretPresentation = null;
            DisposeIfNeeded(presentationTransport, ref errors);
            DisposeIfNeeded(transport, ref errors);
            DisposeIfNeeded(Profiles, ref errors);
            DisposeIfNeeded(Content, ref errors);
            DisposeIfNeeded(Platform, ref errors);
            DisposeIfNeeded(SceneFlow, ref errors);
            DisposeIfNeeded(InputLocks, ref errors);
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

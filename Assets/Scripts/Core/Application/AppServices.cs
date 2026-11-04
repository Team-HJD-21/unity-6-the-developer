// 앱 범위 서비스 의존성과 Match 세션 Factory를 소유합니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Application
{
    public sealed class AppServices : IDisposable
    {
        private bool _isDisposed;
        private readonly RandomService _presentationRandom;
        /// <summary>HUD 흔들림 등 로컬 연출 전용 난수입니다. Match 판정/저장 흐름과 상태를 공유하지 않습니다.</summary>
        public IRandomStreams PresentationRandom => _presentationRandom;

        public IProfileService Profiles { get; }
        public IContentCatalog Content { get; }
        public IPlatformService Platform { get; }
        public ISceneFlow SceneFlow { get; }
        public InputLockService InputLocks { get; }
        public ScreenManager Screens { get; }
        public SceneFlowController SceneTransitions { get; }
        public MatchSessionFactory MatchSessions { get; }
        public ICheckpointStore CheckpointStore { get; }
        public ICheckpointPayloadCodec CheckpointPayloads { get; }
        public INetworkSession NetworkSession { get; private set; }
        public NetworkMatchCoordinator NetworkMatches { get; private set; }
        private IMatchLifecycleTransport _matchTransport;
        public RegionReplicationSession NetworkRegions { get; private set; }
        private IRegionSnapshotTransport _regionTransport;
        public EnvironmentReplicationSession NetworkEnvironments { get; private set; }
        private IMatchEnvironmentTransport _environmentTransport;
        public ITurretCommandTransport NetworkTurretCommands { get; private set; }
        public ITurretPresentationTransport NetworkTurretPresentation { get; private set; }

        public AppServices(IProfileService profiles, IContentCatalog content, IPlatformService platform, ISceneFlow sceneFlow, IAuthority authority,
            INetworkSession networkSession = null, ICheckpointStore checkpointStore = null,
            ICheckpointPayloadCodec checkpointPayloads = null, int? presentationRandomSeed = null)
        {
            if ((checkpointStore == null) != (checkpointPayloads == null))
                throw new ArgumentException("Provide checkpoint storage and payload codec together.");
            Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            SceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            InputLocks = new InputLockService();
            Screens = new ScreenManager(InputLocks);
            SceneTransitions = new SceneFlowController(SceneFlow, InputLocks);
            MatchSessions = new MatchSessionFactory(authority ?? throw new ArgumentNullException(nameof(authority)));
            NetworkSession = networkSession;
            CheckpointStore = checkpointStore;
            CheckpointPayloads = checkpointPayloads;
            _presentationRandom = new RandomService(presentationRandomSeed ?? System.Environment.TickCount, () => !_isDisposed);
        }

        /// <summary>Scene 조립부가 현재 Match runtime과 합의된 writer를 전달해 저장 scope를 생성합니다.</summary>
        /// <remarks>반환 scope는 호출자가 소유하며 Match 종료 때 Dispose합니다. App은 저장 파일 위치/IO 구현만 소유합니다.</remarks>
        public MatchCheckpointScope CreateCheckpointScope(ICheckpointRuntime runtime,
            IEnumerable<ICheckpointParticipant> participants, IEnumerable<string> requiredParticipantKeys)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (CheckpointStore == null) throw new InvalidOperationException("Checkpoint storage is not configured for this app.");
            return new MatchCheckpointScope(runtime, CheckpointStore, CheckpointPayloads, participants, requiredParticipantKeys);
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

        /// <summary>AppRoot가 매 프레임 호출해 공용 전송·최종 결과·읽기 복제를 처리합니다.</summary>
        /// <remarks>Unity 메인 스레드의 App 수명 작업입니다. 필드 게임 시간 정지 중에도 계속 호출합니다.</remarks>
        public void PumpNetwork()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (_matchTransport is INetworkMessagePump pump) pump.Pump();
            NetworkMatches?.FlushPendingCompletion();
            NetworkRegions?.FlushPending();
            if (_regionTransport is INetworkMessagePump regionPump) regionPump.Pump();
            NetworkEnvironments?.FlushPending();
            if (_environmentTransport is INetworkMessagePump environmentPump) environmentPump.Pump();
            if (NetworkTurretCommands is INetworkMessagePump turretPump) turretPump.Pump();
            if (NetworkTurretPresentation is INetworkMessagePump presentationPump) presentationPump.Pump();
        }

        public EnvironmentReplicationSession AttachEnvironmentTransport(IMatchEnvironmentTransport transport)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppServices));
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (NetworkMatches == null || NetworkEnvironments != null)
                throw new InvalidOperationException("Attach one environment transport after the Match lifecycle transport.");
            var environments = new EnvironmentReplicationSession(NetworkSession, NetworkMatches, transport);
            _environmentTransport = transport; NetworkEnvironments = environments;
            return environments;
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
            DisposeIfNeeded(_presentationRandom, ref errors);
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
            var environments = NetworkEnvironments; var environmentTransport = _environmentTransport;
            NetworkEnvironments = null; _environmentTransport = null;
            DisposeIfNeeded(environments, ref errors); DisposeIfNeeded(environmentTransport, ref errors);
            var turretTransport = NetworkTurretCommands; NetworkTurretCommands = null;
            DisposeIfNeeded(turretTransport, ref errors);
            var presentationTransport = NetworkTurretPresentation; NetworkTurretPresentation = null;
            DisposeIfNeeded(presentationTransport, ref errors);
            DisposeIfNeeded(transport, ref errors);
            DisposeIfNeeded(Profiles, ref errors);
            DisposeIfNeeded(Content, ref errors);
            DisposeIfNeeded(Platform, ref errors);
            DisposeIfNeeded(SceneFlow, ref errors);
            DisposeIfNeeded(CheckpointStore, ref errors);
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

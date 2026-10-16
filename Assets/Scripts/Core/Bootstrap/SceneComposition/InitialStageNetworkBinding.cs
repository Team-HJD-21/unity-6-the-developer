// Initial_Stage의 실제 연결과 Host/Client Scene 조립을 App 서비스에 연결합니다.
// 연결 시작·참가자 준비·Match 시작은 별도 API이며 접속 수로 자동 시작하지 않습니다.
// 싱글 진입만 같은 명시 API를 순서대로 호출합니다. Feature 객체 복제는 각 담당의 책임입니다.
using System;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using Unity.Netcode;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public enum InitialStageNetworkEntry { Manual, SinglePlayerHost, Client }

    [DisallowMultipleComponent]
    public sealed class InitialStageNetworkBinding : MonoBehaviour
    {
        [SerializeField] private NetworkManager _manager;
        [SerializeField] private GameObject _matchAnchorPrefab;
        [SerializeField] private InitialStageNetworkEntry _entry = InitialStageNetworkEntry.Manual;
        private AppRoot _app;
        private InitialStageCompositionRoot _scene;
        private NgoNetworkSession _session;
        private NetworkMatchCoordinator _matches;
        private RegionReplicationSession _regions;
        private NetworkMatchAnchor _anchor;
        private IRegionGridSource _anchorSource;
        private NetworkMatchDescriptor _anchorDescriptor;
        private bool _entryPending;
        private bool _disposed;
        public NetworkMatchCoordinator Matches => _matches;
        public INetworkSession Connection => _session;
        public string LastFailure { get; private set; } = string.Empty;

        public void Initialize(AppRoot app, InitialStageCompositionRoot scene)
        {
            if (_app != null) throw new InvalidOperationException("Network Scene is already initialized.");
            if (app == null || scene == null) throw new ArgumentNullException(app == null ? nameof(app) : nameof(scene));
            if (_manager == null) throw new InvalidOperationException("Assign the Scene NetworkManager.");
            _app = app; _scene = scene;
            if (app.NetworkSession == null)
            {
                _session = new NgoNetworkSession(_manager);
                app.AttachNetworkSession(_session);
            }
            else
            {
                _session = app.NetworkSession as NgoNetworkSession ??
                    throw new InvalidOperationException("This Scene requires an NGO App session.");
                if (_session.Manager != _manager)
                {
                    // NGO Scene 동기화로 다시 제작된 이 Scene의 후보만 제거합니다.
                    // 임의의 전역 NetworkManager를 찾아 소유권을 빼앗지 않습니다.
                    NetworkManager duplicate = _manager;
                    if (duplicate.IsListening) throw new InvalidOperationException("A second live Scene connection exists.");
                    duplicate.gameObject.SetActive(false);
                    Destroy(duplicate.gameObject);
                    _manager = _session.Manager;
                }
            }
            if (_manager == null) throw new InvalidOperationException("The App NetworkManager was destroyed.");
            if (_matchAnchorPrefab == null || _matchAnchorPrefab.GetComponent<NetworkMatchAnchor>() == null ||
                _matchAnchorPrefab.GetComponent<NetworkObject>() == null)
                throw new InvalidOperationException("Assign the registered Match anchor prefab.");
            if (!_manager.NetworkConfig.Prefabs.Contains(_matchAnchorPrefab)) _manager.AddNetworkPrefab(_matchAnchorPrefab);
            MonoBehaviour[] consumers = _manager.GetComponents<MonoBehaviour>()
                .Where(behaviour => behaviour is IAppOwnedEncounterMatchConsumer && behaviour.isActiveAndEnabled).ToArray();
            if (consumers.Length != 1) throw new InvalidOperationException("The App manager requires one explicit Encounter consumer.");
            ((IAppOwnedEncounterMatchConsumer)consumers[0]).UseAppNetworkOwnership();
            _scene.SetNetworkEncounterConsumer(consumers[0]);
            _session.StateChanged += OnConnectionState;
            _session.PeerConnected += OnPeerConnected;
            EnsureTransports();
            _entryPending = _session.State == NetworkConnectionState.Disconnected;
        }

        public bool TryStartHost()
        {
            ThrowIfUnavailable();
            bool started = _session.TryStartHost();
            EnsureTransports();
            return started;
        }

        /// <summary>연결 전 진입 흐름을 선택합니다. 연결 중인 세션의 역할을 바꾸지 않습니다.</summary>
        public void ConfigureEntry(InitialStageNetworkEntry entry)
        {
            if (!Enum.IsDefined(typeof(InitialStageNetworkEntry), entry)) throw new ArgumentOutOfRangeException(nameof(entry));
            if (_session != null && _session.State != NetworkConnectionState.Disconnected)
                throw new InvalidOperationException("Choose the entry flow before opening the connection.");
            _entry = entry;
        }

        public bool TryStartClient()
        {
            ThrowIfUnavailable();
            bool started = _session.TryStartClient();
            EnsureTransports();
            return started;
        }

        /// <summary>현재 실제 접속자 1~2명을 고정합니다. 이후 참가자는 이 Match에 합류하지 않습니다.</summary>
        public bool TryPrepareMatch()
        {
            ThrowIfUnavailable();
            EnsureTransports();
            if (!_session.IsServer || _matches == null || _matches.Phase == NetworkMatchPhase.Running) return false;
            ulong[] connections = _session.ConnectedPeerIds.OrderBy(id => id).ToArray();
            if (connections.Length < 1 || connections.Length > 2 || !connections.Contains(_session.LocalConnectionId)) return false;
            if (!connections.SequenceEqual(_manager.ConnectedClientsIds.OrderBy(id => id))) return false;
            var participants = connections.Select(id => new MatchParticipantBinding(id,
                new PlayerId(id == _session.LocalConnectionId ? "local-player" : "peer-" + id),
                id == _session.LocalConnectionId)).ToArray();
            var config = new MatchConfig(new MatchId(Guid.NewGuid().ToString("N")),
                participants.Length == 1 ? MatchMode.Story : MatchMode.Cooperative, new StageId("initial-stage"),
                new DefinitionId("poc-default-difficulty"), "poc", 1,
                participants.Select(binding => new PlayerLoadoutSnapshot(binding.PlayerId, null)));
            RegionSceneBinding region = _scene.RegionBinding;
            if (region == null) throw new InvalidOperationException("The network Scene requires region authoring.");
            RegionGridDefinition definition = region.CaptureInitialDefinition();
            _scene.ValidateNetworkPreparation(new NetworkMatchDescriptor(Guid.NewGuid(), 1, config,
                region.InitialMode, definition, participants));
            return _matches.TryPrepare(config, definition, region.InitialMode, participants);
        }

        public bool TryStartMatch() { ThrowIfUnavailable(); return _matches != null && _matches.TryStart(); }
        public bool TryEndMatch() { ThrowIfUnavailable(); return _matches != null && _matches.TryEnd(); }
        public void ShutdownConnection() { ThrowIfUnavailable(); _session.Shutdown(); }

        private void OnConnectionState(NetworkConnectionState state)
        {
            // App 종료 알림은 Port 재조립 신호가 아닙니다.
            if (state == NetworkConnectionState.Hosting || state == NetworkConnectionState.ConnectedClient ||
                state == NetworkConnectionState.StartingClient) EnsureTransports();
        }

        private void EnsureTransports()
        {
            if (_disposed || _app == null || _manager == null || !_manager.IsListening || _manager.CustomMessagingManager == null) return;
            if (_app.NetworkMatches == null)
            {
                var transport = new NgoMatchLifecycleTransport(_manager);
                try { _app.AttachNetworkMatchTransport(transport); }
                catch { transport.Dispose(); throw; }
            }
            if (_app.NetworkRegions == null)
            {
                var transport = new NgoRegionSnapshotTransport(_manager);
                try { _app.AttachRegionSnapshotTransport(transport); }
                catch { transport.Dispose(); throw; }
            }
            if (ReferenceEquals(_matches, _app.NetworkMatches)) return;
            _matches = _app.NetworkMatches; _regions = _app.NetworkRegions;
            _matches.Preparing += OnPreparing;
            _matches.Started += OnStarted;
            _matches.Ended += OnEnded;
            _regions.SourceChanged += OnRegionSource;
            if (_matches.Phase == NetworkMatchPhase.Preparing) OnPreparing(_matches.Descriptor);
            else if (_matches.Phase == NetworkMatchPhase.Running)
            {
                OnStarted(_matches.Descriptor);
                if (!_session.IsServer && _regions.Source != null) OnRegionSource(_regions.Source);
            }
        }

        private void OnPreparing(NetworkMatchDescriptor descriptor)
        {
            try
            {
                _scene.ValidateNetworkPreparation(descriptor);
                _matches.MarkLocalReady();
            }
            catch (Exception error) { FailMatch(error); throw; }
        }

        private void OnStarted(NetworkMatchDescriptor descriptor)
        {
            try
            {
                _scene.ComposeNetworkMatch(_matches, _session.IsServer, _session.LocalConnectionId);
                if (_session.IsServer)
                {
                    _regions.BindHostSource(_scene.RegionBinding.Source);
                    SpawnAnchor(descriptor, _scene.RegionBinding.Source);
                }
            }
            catch (Exception error) { FailMatch(error); throw; }
        }

        private void FailMatch(Exception error)
        {
            LastFailure = error.Message;
            if (_session.IsServer) _matches.TryEnd();
            else _session.Shutdown();
        }

        private void SpawnAnchor(NetworkMatchDescriptor descriptor, IRegionGridSource source)
        {
            if (_anchor != null) throw new InvalidOperationException("The previous Match anchor is still alive.");
            NetworkObject spawned = NetworkObject.InstantiateAndSpawn(_matchAnchorPrefab, _manager,
                destroyWithScene: true, rotation: Quaternion.identity);
            if (spawned == null) throw new InvalidOperationException("Match anchor spawn was rejected by NGO.");
            _anchor = spawned.GetComponent<NetworkMatchAnchor>();
            _anchorDescriptor = descriptor; _anchorSource = source;
            _anchor.ApplyAuthorityState(descriptor, source.Snapshot);
            _anchorSource.Changed += OnRegionChanged;
        }

        private void OnRegionChanged(RegionGridSnapshot snapshot)
        {
            if (_anchor != null && _anchor.IsSpawned) _anchor.ApplyAuthorityState(_anchorDescriptor, snapshot);
        }

        private void ReleaseAnchor()
        {
            if (_anchorSource != null) _anchorSource.Changed -= OnRegionChanged;
            _anchorSource = null; _anchorDescriptor = null;
            NetworkMatchAnchor anchor = _anchor; _anchor = null;
            if (anchor != null && anchor.IsSpawned && anchor.IsServer) anchor.NetworkObject.Despawn(true);
        }

        private void OnEnded(MatchId matchId)
        {
            try { ReleaseAnchor(); }
            finally { _scene.ReleaseNetworkMatch(matchId); }
        }
        private void OnRegionSource(IRegionGridSource source)
        {
            if (!_session.IsServer && _scene != null) _scene.BindRegionReplica(source);
        }

        public void OnAuthorityMatchReleased(MatchId matchId)
        {
            if (!_disposed && _session != null && _session.IsServer && _matches?.Descriptor?.Config.MatchId == matchId)
                _matches.TryEnd();
        }

        private void OnPeerConnected(ulong id)
        {
            if (!_session.IsServer || id == _session.LocalConnectionId) return;
            // SOS/도중 참가 정책이 없는 기반 단계에서는 고정 cohort 외 접속을 허용하지 않습니다.
            if (_manager.ConnectedClientsIds.Count > 2 ||
                (_matches?.Descriptor != null && !_matches.TryResolveParticipant(id, out _)))
                _manager.DisconnectClient(id);
        }

        private void Update()
        {
            if (!_entryPending || _disposed || _app == null) return;
            _entryPending = false;
            try
            {
                if (_entry == InitialStageNetworkEntry.Client) TryStartClient();
                else if (_entry == InitialStageNetworkEntry.SinglePlayerHost && TryStartHost())
                {
                    if (!TryPrepareMatch() || !TryStartMatch())
                        throw new InvalidOperationException("Single-player Host could not prepare/start its Match.");
                }
            }
            catch (Exception error) { LastFailure = error.Message; Debug.LogException(error, this); }
        }

        private void OnDestroy()
        {
            if (_disposed) return; _disposed = true;
            if (_session != null)
            {
                _session.StateChanged -= OnConnectionState;
                _session.PeerConnected -= OnPeerConnected;
            }
            if (_matches != null)
            {
                _matches.Preparing -= OnPreparing; _matches.Started -= OnStarted; _matches.Ended -= OnEnded;
            }
            if (_regions != null) _regions.SourceChanged -= OnRegionSource;
            try { ReleaseAnchor(); }
            finally
            {
                if (_matches != null && _session.IsServer && _matches.Phase != NetworkMatchPhase.Disposed) _matches.TryEnd();
            }
        }

        private void ThrowIfUnavailable()
        {
            if (_disposed || _app == null || _session == null) throw new InvalidOperationException("Initialize the network Scene first.");
        }
    }
}

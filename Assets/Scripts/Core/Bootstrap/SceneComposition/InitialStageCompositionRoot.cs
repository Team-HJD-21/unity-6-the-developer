// Initial_Stage에서 Match·Player를 조립하고 Encounter에 공간 조회 경로를 연결합니다.
// Scene 수명에 맞춰 actor와 Match, Encounter 바인딩을 정리합니다.

using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Content.Authoring;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class InitialStageCompositionRoot : MonoBehaviour, ISceneCompositionRoot
    {
        private static readonly PlayerId LocalPlayerId = new PlayerId("local-player");
        private const float BattlefieldSampleInterval = 0.1f;

        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform actorParent;
        [SerializeField] private BattlefieldGridSettings battlefieldGridSettings;
        [Tooltip("IEncounterMatchConsumer 구현체. 기존 Scene 호환을 위해 미지정 시 활성 소비자 하나를 조회합니다.")]
        [SerializeField] private MonoBehaviour _encounterConsumerBehaviour;
        [SerializeField] private InitialStageNetworkBinding _networkBinding;

        private IAppMatchHost _matchHost;
        private MatchSession _matchSession;
        private MatchCoordinator _coordinator;
        private MatchTimeService _matchTime;
        private IMatchWeatherSource _weatherSource;
        private IMatchTimeOfDaySource _timeOfDaySource;
        private IDisposable _environmentScope;
        private IMatchEnvironmentSource _environmentReplica;
        public MatchCoordinator MatchCoordinator => _coordinator;
        /// <summary>현재 권위 Match에 주입된 공용 게임 시계입니다. Client 표현 시계는 별도 복제 경계에서 연결합니다.</summary>
        public IMatchTime MatchTime => _matchTime?.Clock;
        /// <summary>바인딩된 Feature에 전달할 게임 시간 예약 API입니다. 완료 후 새 예약은 거부합니다.</summary>
        public IMatchScheduler MatchTimers => _matchTime?.Scheduler;
        public IMatchEnvironmentSource EnvironmentSource => _matchSession?.Environment ?? _environmentReplica;
        public double LastSpatialRefreshMilliseconds { get; private set; } = double.NaN;
        public double LastTopologyRefreshMilliseconds { get; private set; } = double.NaN;
        private static readonly Unity.Profiling.ProfilerMarker SpatialMarker = new Unity.Profiling.ProfilerMarker("TeamHJD.Battlefield.SampleAndPublish");
        private static readonly Unity.Profiling.ProfilerMarker TopologyMarker = new Unity.Profiling.ProfilerMarker("TeamHJD.Battlefield.TopologyAndPublish");
        private RegionSceneBinding _regionBinding;
        private TurretMatchBinding _turretBinding;
        private MatchPlayerActorScope _playerActors;
        private ILocalPlayerActorConsumer[] _localActorConsumers = Array.Empty<ILocalPlayerActorConsumer>();
        private IBattlefieldTurretInputSource _turretInputSource;
        private IEncounterMatchConsumer _encounterConsumer;
        private IBattlefieldEnemyPositionSource[] _enemyPositionSources = Array.Empty<IBattlefieldEnemyPositionSource>();
        private readonly Dictionary<TeamHJD.Game.Domain.EntityId, int> _lastEntityCells =
            new Dictionary<TeamHJD.Game.Domain.EntityId, int>();
        private float _nextBattlefieldSampleTime;
        private bool _ownsMatch;
        private MatchId _ownedMatchId;

        private bool HasCurrentMatch => _ownsMatch && _matchHost != null &&
            _matchHost.CurrentMatchId == _ownedMatchId;

        public void Compose(IAppMatchHost appMatchHost)
        {
            if (_matchHost != null) throw new InvalidOperationException("Initial Stage has already been composed.");
            if (appMatchHost == null) throw new ArgumentNullException(nameof(appMatchHost));
            PrepareEnvironmentSources();
            if (_networkBinding != null)
            {
                if (!(appMatchHost is AppRoot app))
                    throw new InvalidOperationException("The network Scene requires its owning AppRoot.");
                _matchHost = appMatchHost;
                _networkBinding.Initialize(app, this);
                return;
            }
            ComposeAuthority(appMatchHost);
        }

        private void ComposeAuthority(IAppMatchHost appMatchHost, NetworkMatchCoordinator network = null,
            ulong connectionId = 0)
        {
            if (_ownsMatch || _playerActors != null) throw new InvalidOperationException("The Scene already has Match actors.");
            if (playerPrefab == null) throw new InvalidOperationException("Assign the Stage_1 Player prefab before entering Play Mode.");
            if (battlefieldGridSettings == null) throw new InvalidOperationException("Assign shared BattlefieldGridSettings before entering Play Mode.");

            PlayerStart[] starts = FindPlayerStarts();
            if (starts.Length == 0) throw new InvalidOperationException("Initial_Stage requires at least one active PlayerStart.");
            MatchCameraFollow cameraFollow = FindSceneCameraFollow();
            if (cameraFollow == null) throw new InvalidOperationException("Initial_Stage requires a MatchCameraFollow on its Main Camera.");
            RegionSceneBinding regionBinding = ValidateRegionAuthoring();

            MatchId matchId = network?.Descriptor.Config.MatchId ?? new MatchId(Guid.NewGuid().ToString("N"));
            PlayerId localPlayerId = LocalPlayerId;
            int localSlot = 0;
            if (network != null && (!network.TryResolveParticipant(connectionId, out localPlayerId) ||
                !network.Descriptor.TryGetSlot(connectionId, out localSlot)))
                throw new InvalidOperationException("Local connection is outside the prepared Match.");
            var loadout = new PlayerLoadoutSnapshot(LocalPlayerId, new Dictionary<DefinitionId, int>());
            MatchConfig config = network?.Descriptor.Config ?? new MatchConfig(
                matchId,
                MatchMode.Story,
                new StageId("initial-stage"),
                new DefinitionId("poc-default-difficulty"),
                "poc",
                1,
                new[] { loadout });
            var initialState = new MatchState(
                matchId,
                MatchPhase.Preparing,
                config.Players.Select(player => new PlayerState(player.PlayerId, 400, 400, true)),
                new ControlUnitState(1500, 1500, 250, 250),
                new WaveState(0, 1, 0, 0),
                Array.Empty<TurretState>(),
                Array.Empty<EnemyState>(),
                Array.Empty<SectorState>());

            PlayerStart firstStart = starts.Single(start => start.SlotIndex == localSlot);
            IBattlefieldTurretInputSource turretInputSource = FindTurretInputSource();
            BattlefieldSpatialInput turretBattlefieldInput = turretInputSource != null
                ? turretInputSource.CaptureTurretLayout()
                : BattlefieldSpatialInput.Empty;
            if (turretInputSource == null)
                Debug.LogError("Initial_Stage has no IBattlefieldTurretInputSource; Territory/Frontline will be empty.", this);
            _enemyPositionSources = FindEnemyPositionSources();
            var dynamicBattlefieldInput = new BattlefieldDynamicSpatialInput(
                new[]
                {
                    new PlayerSpatialInput(
                        new TeamHJD.Game.Domain.EntityId(localPlayerId.Value),
                        new BattlefieldPoint(firstStart.Position.x, firstStart.Position.y))
                },
                Array.Empty<EnemySpatialInput>());
            BattlefieldGridConfiguration grid = battlefieldGridSettings.CreateConfiguration();

            try
            {
                _turretBinding = GetComponent<TurretMatchBinding>();
                _turretBinding?.BindMatch(matchId, () => HasCurrentMatch && _matchSession != null &&
                    !_matchSession.IsDisposed && _matchSession.State.Phase == MatchPhase.Running &&
                    (network == null || network.IsGameplayRunning));
                MatchSession session = appMatchHost.StartMatch(
                    config,
                    initialState,
                    new StoryModeRules(),
                    turretBattlefieldInput,
                    dynamicBattlefieldInput,
                    grid);
                _matchHost = appMatchHost;
                _ownsMatch = true;
                _ownedMatchId = matchId;
                _matchSession = session;
                // D의 Match lease가 쓰기를 허용한 뒤, Core 지역 정책을 붙이기 전에 초기화합니다.
                ConfigureUnlockZones(regionBinding, network?.Descriptor.RegionMode ?? regionBinding.InitialMode);
                var controlUnitHealth = regionBinding.ControlUnit.GetComponents<MonoBehaviour>()
                    .Where(component => component != null && component.isActiveAndEnabled).OfType<IControlUnitHealthSource>().Single();
                ITimestampSource timestamp = network == null ? (ITimestampSource)new UnityTimestampSource() :
                    new NgoTimestampSource((_networkBinding.Connection as NgoNetworkSession)?.Manager ??
                        throw new InvalidOperationException("The authority Match requires its composed NGO connection."));
                _matchTime = new MatchTimeService(session, timestamp,
                    () => HasCurrentMatch && (network == null || network.IsGameplayRunning));
                _coordinator = new MatchCoordinator(session, controlUnitHealth,
                    () => HasCurrentMatch && (network == null || network.IsGameplayRunning), _matchTime.Clock);
                _environmentScope = new MatchEnvironmentSceneScope(session, _coordinator, _weatherSource, _timeOfDaySource,
                    () => HasCurrentMatch && session.IsGameplayRunning && (network == null || network.IsGameplayRunning));
                session.InitializeSpawnSources(
                    () => HasCurrentMatch && session.IsGameplayRunning && (network == null || network.IsGameplayRunning),
                    _coordinator.GameplayCancellation);
                _turretInputSource = turretInputSource;
                if (_turretInputSource != null)
                    _turretInputSource.LayoutChanged += OnTurretLayoutChanged;
                _localActorConsumers = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .OfType<ILocalPlayerActorConsumer>().ToArray();
                foreach (ILocalPlayerActorConsumer consumer in _localActorConsumers)
                    consumer.PrepareLocalActor(matchId, localPlayerId);
                var spawner = new LocalPlayerActorSpawner(playerPrefab, actorParent);
                _playerActors = network == null ? MatchPlayerActorScope.SpawnPlayers(session, spawner, starts) :
                    MatchPlayerActorScope.SpawnLocalPlayer(network, connectionId, spawner, starts, session);
                _matchSession = session;
                foreach (ILocalPlayerActorConsumer consumer in _localActorConsumers)
                    consumer.BindLocalActor(matchId, localPlayerId, _playerActors.Actors[localPlayerId]);
                BindLocalInput(_playerActors.Actors[localPlayerId]);
                _regionBinding = regionBinding;
                if (network == null) _regionBinding?.BindMatch(session, entityIds: _turretBinding);
                else
                {
                    if (_regionBinding == null) throw new InvalidOperationException("Network Match requires a RegionSceneBinding.");
                    _regionBinding.BindMatch(session, network.Descriptor.RegionGrid,
                        network.Descriptor.RegionMode, () => network.IsGameplayRunning, _turretBinding);
                }
                _encounterConsumer = ResolveEncounterConsumer();
                if (_encounterConsumer != null)
                {
                    if (!(_encounterConsumer is IEncounterEnvironmentConsumer environmentConsumer))
                        throw new InvalidOperationException("The Stage Encounter consumer must support the explicit environment query port.");
                    environmentConsumer.BindEnvironment(matchId, () => HasCurrentMatch && _matchSession != null &&
                        _matchSession.IsGameplayRunning ? _matchSession.Environment?.Snapshot : null);
                    if (!(_encounterConsumer is IEncounterSpawnContextConsumer spawnConsumer))
                        throw new InvalidOperationException("Encounter must explicitly consume the current Match spawn scope.");
                    spawnConsumer.BindSpawnContext(matchId, session.SpawnSources, _coordinator, config.RandomSeed, _coordinator.GameplayCancellation);
                    Func<BattlefieldSpatialSnapshot> latest = () => HasCurrentMatch && _matchSession != null &&
                        !_matchSession.IsDisposed && _matchSession.State.Phase == MatchPhase.Running ? _matchSession.Battlefield : null;
                    if (network != null)
                    {
                        if (!(_encounterConsumer is IAppOwnedEncounterMatchConsumer managed))
                            throw new InvalidOperationException("Network Encounter consumer must support App connection ownership.");
                        managed.UseAppNetworkOwnership();
                        managed.BindAuthorityMatch(matchId, localPlayerId, latest);
                    }
                    else _encounterConsumer.BindMatch(matchId, latest);
                }
                else
                    Debug.LogWarning("Initial_Stage has no IEncounterMatchConsumer; automatic Encounter is unavailable.", this);
                _lastEntityCells.Clear();
                _nextBattlefieldSampleTime = 0f;
                cameraFollow.Bind(_playerActors.Actors[localPlayerId].transform);
                session.Disposing += ReleaseMatchBindings;
                _coordinator.Completed += OnMatchCompleted;
                _coordinator.Start();
                BattlefieldSpatialSnapshot battlefield = session.Battlefield;
                Debug.Log(
                    $"Initial_Stage Match started ({matchId}); spawned {_playerActors.Actors.Count} Player actor(s). " +
                    $"Battlefield: {battlefield.Vertices.Count} turret point(s), {battlefield.Triangles.Count} triangle(s), " +
                    $"{battlefield.FrontlineEdges.Count} frontline edge(s), {battlefield.Grid.OccupiedCells.Count} occupied grid cell(s).",
                    this);
                if (battlefield.Triangles.Count == 0)
                    Debug.LogWarning(
                        "Battlefield Territory/Frontline was not formed. Check that at least three operational turrets " +
                        "are registered at non-collinear XY positions inside the authored grid bounds.",
                        this);
            }
            catch (Exception error)
            {
                try { ReleaseOwnedBindings(matchId, endOwnedMatch: true); }
                catch (Exception cleanupError) { throw new AggregateException("Scene setup and rollback failed.", error, cleanupError); }
                throw;
            }
        }

        public RegionSceneBinding RegionBinding => GetComponent<RegionSceneBinding>();
        public IReadOnlyDictionary<PlayerId, GameObject> LocalPlayerActors => _playerActors?.Actors;

        public void SetNetworkEncounterConsumer(MonoBehaviour consumer)
        {
            if (_ownsMatch || _playerActors != null) throw new InvalidOperationException("Set the consumer before Match composition.");
            if (consumer == null || !consumer.isActiveAndEnabled || !(consumer is IAppOwnedEncounterMatchConsumer))
                throw new ArgumentException("An active App-owned Encounter consumer is required.", nameof(consumer));
            _encounterConsumerBehaviour = consumer;
            var configurations = FindSceneBehaviours().OfType<IEncounterSceneConfiguration>().ToArray();
            if (configurations.Length != 1)
                throw new InvalidOperationException("The Match Scene requires one explicit Encounter execution configuration.");
            configurations[0].ConfigureEncounter((IEncounterMatchConsumer)consumer);
        }

        /// <summary>준비 응답 전에 양쪽 Scene의 슬롯·콘텐츠·필수 제작 연결을 검사합니다.</summary>
        public void ValidateNetworkPreparation(NetworkMatchDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Config.StageId.Value != "initial-stage" || descriptor.Config.ContentVersion != "poc")
                throw new InvalidOperationException("The prepared Stage/content version is not supported by this Scene.");
            if (playerPrefab == null || battlefieldGridSettings == null || FindSceneCameraFollow() == null || RegionBinding == null ||
                GetComponent<TurretMatchBinding>() == null)
                throw new InvalidOperationException("Player, Grid, camera and region Scene references are required.");
            ValidateRegionAuthoring();
            PlayerStart[] starts = FindPlayerStarts();
            for (int slot = 0; slot < descriptor.Config.Players.Count; slot++)
                if (starts.Count(start => start.SlotIndex == slot) != 1)
                    throw new InvalidOperationException($"Expected exactly one active PlayerStart for slot {slot}.");
        }

        private RegionSceneBinding ValidateRegionAuthoring()
        {
            RegionSceneBinding[] bindings = FindObjectsByType<RegionSceneBinding>(FindObjectsSortMode.None)
                .Where(binding => binding.gameObject.scene == gameObject.scene && binding.isActiveAndEnabled).ToArray();
            if (bindings.Length != 1) throw new InvalidOperationException("Initial_Stage requires exactly one active RegionSceneBinding.");
            RegionSceneBinding region = bindings[0];
            TurretMatchBinding turrets = GetComponent<TurretMatchBinding>();
            if (turrets == null || !turrets.isActiveAndEnabled || turrets.Controller == null ||
                region.Controller != turrets.Controller || region.Controller.gameObject.scene != gameObject.scene)
                throw new InvalidOperationException("Region and turret Match bindings must use the same active Scene controller.");
            if (!region.Controller.isActiveAndEnabled || region.ControlUnit == null ||
                region.ControlUnit.gameObject.scene != gameObject.scene || !region.ControlUnit.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Assign an active Scene CU and FieldTurretController before preparing the Match.");
            // 정의 생성은 읽기 전용입니다. CU의 유효 좌표·Grid 범위·Inspector 구역 수를 검사합니다.
            var health = region.ControlUnit.GetComponents<MonoBehaviour>()
                .Where(component => component != null && component.isActiveAndEnabled).OfType<IControlUnitHealthSource>().ToArray();
            if (health.Length != 1 || health[0].MaxHealth <= 0)
                throw new InvalidOperationException("Provide one configured, active CU health source before Match preparation.");
            region.Controller.CreateUnlockZoneDefinition(region.ControlUnit.position);
            return region;
        }

        private void ConfigureUnlockZones(RegionSceneBinding region, RegionPolicyMode mode)
        {
            if (_turretBinding.Controller.BoundMatchId != _ownedMatchId || !_turretBinding.Controller.HasWriteAuthority)
                throw new InvalidOperationException("CU initialization requires the current authoritative turret Match lease.");
            if (mode == RegionPolicyMode.Static)
            {
                if (!_turretBinding.Controller.ConfigureUnlockZones(region.ControlUnit.position))
                    throw new InvalidOperationException("CU unlock-zone initialization could not apply every owned turret restriction.");
            }
            else if (mode != RegionPolicyMode.Dynamic) throw new ArgumentOutOfRangeException(nameof(mode));
            // Dynamic은 임시 삼각형에서 점령 셀을 만들므로 정적 잠금으로 기존 활성 상태를 변경하지 않습니다.
        }

        private void OnMatchCompleted(MatchCompletion completion)
        {
            if (!HasCurrentMatch || completion.Result.MatchId != _ownedMatchId) return;
            var errors = new List<Exception>();
            RunCleanup(() =>
            {
                if (_networkBinding != null && !_networkBinding.Matches.TryPublishCompletion(completion))
                    throw new InvalidOperationException("The current Host could not commit its final Match result.");
            }, errors);
            // 불변 결과를 보존/게시한 후 실제 Feature 작업을 닫습니다. 연결·Session은 결과 인수까지 유지합니다.
            RunCleanup(() => ReleaseEncounterConsumer(completion.Result.MatchId), errors);
            RunCleanup(() => _turretBinding?.StopMatchOperations(), errors);
            if (errors.Count > 0) throw new AggregateException("Match completed; publication or Feature shutdown failed.", errors);
        }

        public void ComposeNetworkMatch(NetworkMatchCoordinator matches, bool isServer, ulong connectionId)
        {
            ValidateNetworkPreparation(matches?.Descriptor);
            if (isServer) { ComposeAuthority(_matchHost, matches, connectionId); return; }
            if (_playerActors != null) throw new InvalidOperationException("Client actors are already composed.");
            if (!matches.TryResolveParticipant(connectionId, out PlayerId playerId))
                throw new InvalidOperationException("The Client connection is not a participant.");
            _ownedMatchId = matches.Descriptor.Config.MatchId;
            try
            {
                _turretBinding = GetComponent<TurretMatchBinding>();
                _turretBinding.BindMatch(_ownedMatchId, () => false);
                _localActorConsumers = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .OfType<ILocalPlayerActorConsumer>().ToArray();
                foreach (var consumer in _localActorConsumers) consumer.PrepareLocalActor(_ownedMatchId, playerId);
                _playerActors = MatchPlayerActorScope.SpawnLocalPlayer(matches, connectionId,
                    new LocalPlayerActorSpawner(playerPrefab, actorParent), FindPlayerStarts());
                foreach (var consumer in _localActorConsumers)
                    consumer.BindLocalActor(_ownedMatchId, playerId, _playerActors.Actors[playerId]);
                BindLocalInput(_playerActors.Actors[playerId]);
                FindSceneCameraFollow().Bind(_playerActors.Actors[playerId].transform);
            }
            catch (Exception error)
            {
                try { ReleaseNetworkMatch(_ownedMatchId); }
                catch (Exception cleanupError) { throw new AggregateException("Client Scene setup and rollback failed.", error, cleanupError); }
                throw;
            }
        }

        public void BindRegionReplica(IRegionGridSource source)
        {
            _regionBinding = RegionBinding;
            _regionBinding.UnbindMatch();
            if (source != null) _regionBinding.BindReplica(source);
        }

        public void BindEnvironmentReplica(IMatchEnvironmentSource source)
        {
            IDisposable previous = _environmentScope; _environmentScope = null; _environmentReplica = null;
            previous?.Dispose();
            if (source == null) return;
            if (_ownsMatch || _ownedMatchId.IsEmpty) throw new InvalidOperationException("Bind environment replicas only to a composed Client Match.");
            _environmentScope = new MatchEnvironmentReplicaBinding(_ownedMatchId, source, _weatherSource, _timeOfDaySource);
            _environmentReplica = source;
        }

        private void PrepareEnvironmentSources()
        {
            var weather = FindSceneBehaviours().OfType<IMatchWeatherSource>().ToArray();
            var time = FindSceneBehaviours().OfType<IMatchTimeOfDaySource>().ToArray();
            if (weather.Length > 1 || time.Length > 1) throw new InvalidOperationException("Keep one authoritative weather/time source per Match Scene.");
            _weatherSource = weather.FirstOrDefault(); _timeOfDaySource = time.FirstOrDefault();
            foreach (var feature in MatchEnvironmentSceneScope.Collect(_weatherSource, _timeOfDaySource)) feature.RequireMatchScope();
        }

        private void BindLocalInput(GameObject actor)
        {
            if (!(_matchHost is AppRoot app)) return;
            foreach (var consumer in actor.GetComponents<MonoBehaviour>().OfType<ILocalInputConsumer>())
                consumer.BindInputBlocker(app.Services.InputLocks);
        }

        public void ReleaseNetworkMatch(MatchId matchId)
        {
            if (_ownedMatchId != matchId) return;
            if (_ownsMatch) { _matchHost.TryEndMatch(matchId); return; }
            ReleaseOwnedBindings(matchId);
        }

        private void OnDestroy()
        {
            try { ReleaseOwnedBindings(_ownedMatchId, endOwnedMatch: true); }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally { _matchHost = null; }
        }

        private void ReleaseMatchBindings()
        {
            MatchSession session = _matchSession;
            if (session == null) return;
            ReleaseOwnedBindings(session.Config.MatchId, notifyNetworkEnd: true);
        }

        private void ReleaseOwnedBindings(MatchId matchId, bool endOwnedMatch = false, bool notifyNetworkEnd = false)
        {
            if (_matchSession != null) _matchSession.Disposing -= ReleaseMatchBindings;
            MatchPlayerActorScope actors = _playerActors;
            RegionSceneBinding regions = _regionBinding;
            TurretMatchBinding turrets = _turretBinding;
            MatchCoordinator coordinator = _coordinator;
            MatchTimeService time = _matchTime;
            IDisposable environment = _environmentScope;
            IBattlefieldTurretInputSource input = _turretInputSource;
            IAppMatchHost host = _matchHost;
            bool ownedMatch = _ownsMatch;
            // 콜백의 실패/재진입과 관계없이 이전 Scene 소유권은 먼저 닫습니다.
            _matchSession = null;
            _coordinator = null;
            _matchTime = null;
            _environmentScope = null; _environmentReplica = null;
            _playerActors = null; _regionBinding = null; _turretBinding = null; _turretInputSource = null;
            _ownsMatch = false;
            _ownedMatchId = default;
            _enemyPositionSources = Array.Empty<IBattlefieldEnemyPositionSource>();
            _lastEntityCells.Clear();
            var errors = new List<Exception>();
            RunCleanup(() => coordinator?.Dispose(), errors);
            RunCleanup(() => environment?.Dispose(), errors);
            RunCleanup(() => ReleaseLocalActorConsumers(matchId), errors);
            RunCleanup(() => ReleaseEncounterConsumer(matchId), errors);
            RunCleanup(() => actors?.Dispose(), errors);
            RunCleanup(() => { if (regions != null) regions.UnbindMatch(); }, errors);
            RunCleanup(() => { if (turrets != null) turrets.UnbindMatch(matchId); }, errors);
            RunCleanup(() => { if (input != null) input.LayoutChanged -= OnTurretLayoutChanged; }, errors);
            RunCleanup(() => time?.Dispose(), errors);
            if (endOwnedMatch && ownedMatch) RunCleanup(() => host?.TryEndMatch(matchId), errors);
            if (notifyNetworkEnd) RunCleanup(() => _networkBinding?.OnAuthorityMatchReleased(matchId), errors);
            if (errors.Count > 0) throw new AggregateException("Scene cleanup failed after all owned bindings were released.", errors);
        }

        private static void RunCleanup(Action cleanup, List<Exception> errors)
        {
            try { cleanup(); }
            catch (Exception error) { errors.Add(error); }
        }

        private PlayerStart[] FindPlayerStarts()
        {
            return FindObjectsByType<PlayerStart>(FindObjectsSortMode.None)
                .Where(start => start.gameObject.scene == gameObject.scene && start.isActiveAndEnabled)
                .ToArray();
        }

        private void ReleaseLocalActorConsumers(MatchId matchId)
        {
            ILocalPlayerActorConsumer[] consumers = _localActorConsumers;
            _localActorConsumers = Array.Empty<ILocalPlayerActorConsumer>();
            foreach (ILocalPlayerActorConsumer consumer in consumers)
            {
                if (consumer is MonoBehaviour behaviour && behaviour == null) continue;
                try { consumer.UnbindLocalActor(matchId); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private IEncounterMatchConsumer ResolveEncounterConsumer()
        {
            if (_encounterConsumerBehaviour != null)
            {
                if (!_encounterConsumerBehaviour.isActiveAndEnabled ||
                    !(_encounterConsumerBehaviour is IEncounterMatchConsumer consumer))
                    throw new InvalidOperationException("Assigned Encounter consumer must be active and implement IEncounterMatchConsumer.");
                return consumer;
            }

            // 기존 Scene의 NGO 루트는 Awake에서 DDOL로 이동합니다. 직렬화 연결 전까지만
            // 전역 조회를 허용하되 복수 소비자는 임의로 선택하지 않고 오류로 처리합니다.
            IEncounterMatchConsumer[] consumers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(component => component != null && component.isActiveAndEnabled)
                .OfType<IEncounterMatchConsumer>().ToArray();
            if (consumers.Length > 1)
                throw new InvalidOperationException("Assign an explicit Encounter consumer when more than one is active.");
            return consumers.FirstOrDefault();
        }

        private void ReleaseEncounterConsumer(MatchId matchId)
        {
            IEncounterMatchConsumer consumer = _encounterConsumer;
            _encounterConsumer = null;
            try
            {
                try { consumer?.UnbindMatch(matchId); }
                finally
                {
                    try { (consumer as IEncounterEnvironmentConsumer)?.UnbindEnvironment(matchId); }
                    finally { (consumer as IEncounterSpawnContextConsumer)?.UnbindSpawnContext(matchId); }
                }
            }
            catch (Exception exception)
            {
                // Feature 해제 실패가 Player/Core Match 정리를 중단해서는 안 됩니다.
                Debug.LogException(exception, this);
            }
        }

        private MatchCameraFollow FindSceneCameraFollow()
        {
            return FindObjectsByType<MatchCameraFollow>(FindObjectsSortMode.None)
                .FirstOrDefault(camera => camera.gameObject.scene == gameObject.scene);
        }

        private IBattlefieldTurretInputSource FindTurretInputSource()
        {
            MonoBehaviour source = FindSceneBehaviours()
                .FirstOrDefault(component => component is IBattlefieldTurretInputSource);
            return source as IBattlefieldTurretInputSource;
        }

        private IEnumerable<MonoBehaviour> FindSceneBehaviours()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component != null && component.isActiveAndEnabled)
                        yield return component;
                }
            }
        }

        private void OnTurretLayoutChanged()
        {
            if (!HasCurrentMatch || _turretInputSource == null) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                using (TopologyMarker.Auto()) _matchHost.UpdateBattlefieldTurretLayout(_turretInputSource.CaptureTurretLayout());
            }
            finally { LastTopologyRefreshMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency; }
        }

        private IBattlefieldEnemyPositionSource[] FindEnemyPositionSources()
        {
            return FindSceneBehaviours()
                .OfType<IBattlefieldEnemyPositionSource>()
                .ToArray();
        }

        private void Update()
        {
            _matchTime?.Pump(Time.timeScale);
            if (!HasCurrentMatch || _matchSession == null || _playerActors == null || Time.unscaledTime < _nextBattlefieldSampleTime)
                return;
            if (!_matchSession.IsGameplayRunning) return;

            _nextBattlefieldSampleTime = Time.unscaledTime + BattlefieldSampleInterval;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { using (SpatialMarker.Auto()) RefreshDynamicInput(); }
            finally { LastSpatialRefreshMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency; }
        }

        private void RefreshDynamicInput()
        {
            var players = new List<PlayerSpatialInput>(_playerActors.Actors.Count);
            foreach (KeyValuePair<PlayerId, GameObject> actor in _playerActors.Actors)
            {
                if (actor.Value == null) continue;
                Vector3 position = actor.Value.transform.position;
                players.Add(new PlayerSpatialInput(
                    new TeamHJD.Game.Domain.EntityId(actor.Key.Value),
                    new BattlefieldPoint(position.x, position.y)));
            }

            var enemies = new List<EnemySpatialInput>();
            foreach (IBattlefieldEnemyPositionSource source in _enemyPositionSources)
            {
                IReadOnlyList<EnemySpatialInput> captured = source?.CaptureEnemyPositions();
                if (captured == null) continue;
                for (int index = 0; index < captured.Count; index++)
                    if (captured[index] != null) enemies.Add(captured[index]);
            }

            var nextInput = new BattlefieldDynamicSpatialInput(players, enemies);
            Dictionary<TeamHJD.Game.Domain.EntityId, int> nextCells = GetOccupiedCellByEntity(nextInput);
            if (HaveSameCellOccupancy(_lastEntityCells, nextCells)) return;

            // App 경계를 거쳐야 Editor의 snapshot 변경 통지도 같은 revision을 받습니다.
            _matchHost.UpdateBattlefieldParticipants(nextInput);
            _lastEntityCells.Clear();
            foreach (KeyValuePair<TeamHJD.Game.Domain.EntityId, int> cell in nextCells)
                _lastEntityCells.Add(cell.Key, cell.Value);
        }

        private Dictionary<TeamHJD.Game.Domain.EntityId, int> GetOccupiedCellByEntity(
            BattlefieldDynamicSpatialInput input)
        {
            var cellsByEntity = new Dictionary<TeamHJD.Game.Domain.EntityId, int>();
            BattlefieldGridConfiguration configuration = _matchSession.Battlefield.Grid.Configuration;
            foreach (PlayerSpatialInput player in input.Players)
            {
                bool inBounds = configuration.TryMapPoint(player.Position, out int x, out int y);
                cellsByEntity.Add(player.EntityId, inBounds ? y * configuration.CellsX + x : -1);
            }
            foreach (EnemySpatialInput enemy in input.Enemies)
            {
                bool inBounds = configuration.TryMapPoint(enemy.Position, out int x, out int y);
                cellsByEntity.Add(enemy.EntityId, inBounds ? y * configuration.CellsX + x : -1);
            }
            return cellsByEntity;
        }

        private static bool HaveSameCellOccupancy(
            Dictionary<TeamHJD.Game.Domain.EntityId, int> previous,
            Dictionary<TeamHJD.Game.Domain.EntityId, int> next)
        {
            if (previous.Count != next.Count) return false;
            foreach (KeyValuePair<TeamHJD.Game.Domain.EntityId, int> entity in next)
                if (!previous.TryGetValue(entity.Key, out int previousCell) || previousCell != entity.Value)
                    return false;
            return true;
        }
    }
}

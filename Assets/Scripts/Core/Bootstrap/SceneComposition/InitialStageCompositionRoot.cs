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
    [DefaultExecutionOrder(-300)]
    public sealed partial class InitialStageCompositionRoot : MonoBehaviour, ISceneCompositionRoot
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
        private IMatchTime _timeReplica;
        private MatchFieldReplicaScope _replicaField;
        private MatchFieldSceneScope _fieldScope;
        private RandomConsumerScope _randomConsumers;
        /// <summary>권위 Match의 범용 난수 흐름입니다. 초기화 전·Client·Match 해제 후에는 null입니다.</summary>
        public IRandomStreams Random => _matchSession?.Random;
        /// <summary>저장 조립부가 실제 Feature writer 목록에 함께 등록할 Core 난수 writer를 생성합니다.</summary>
        public ICheckpointParticipant CreateRandomCheckpointParticipant() => _matchSession?.CreateRandomCheckpointParticipant() ??
            throw new InvalidOperationException("No current authority Match exists.");
        /// <summary>권위 Match 참가자의 확정 위치입니다. Client 신고용 쓰기 API는 제공하지 않습니다.</summary>
        public MatchPresenceService MatchPresence => _fieldScope?.Presence;
        /// <summary>Player 생성자가 Host에 만든 실제 원격 actor를 등록합니다. actor 제거 시 lease를 해제합니다.</summary>
        public IDisposable RegisterAuthorityPresence(PlayerId player, GameObject actor)
            => RegisterActorCheckpointScope(player, actor);
        private IMatchWeatherSource _weatherSource;
        private IMatchTimeOfDaySource _timeOfDaySource;
        private IDisposable _environmentScope;
        private IMatchEnvironmentSource _environmentReplica;
        public MatchCoordinator MatchCoordinator => _coordinator;
        /// <summary>현재 Match의 읽기 시계입니다. Client는 첫 권위 anchor를 받기 전 null입니다.</summary>
        public IMatchTime MatchTime => _matchTime?.Clock ?? _timeReplica;
        /// <summary>바인딩된 Feature에 전달할 게임 시간 예약 API입니다. 완료 후 새 예약은 거부합니다.</summary>
        public IMatchScheduler MatchTimers => _matchTime?.Scheduler;
        /// <summary>Game Debugger와 통합 검사가 조회하는 현재 권위 Match의 유효 예약 수입니다.</summary>
        public int PendingMatchTimers => _matchTime?.PendingTimers ?? 0;
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
            if (network == null && appMatchHost is AppRoot appArrival)
                config = appArrival.ConfigureCheckpointArrival(gameObject.scene.path, config);
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
                    .Where(component => component != null && component.isActiveAndEnabled).OfType<IControlUnitVitals>().Single();
                ITimestampSource timestamp = network == null ? (ITimestampSource)new UnityTimestampSource() :
                    new NgoTimestampSource((_networkBinding.Connection as NgoNetworkSession)?.Manager ??
                        throw new InvalidOperationException("The authority Match requires its composed NGO connection."));
                _matchTime = new MatchTimeService(session, timestamp,
                    () => HasCurrentMatch && (network == null || network.IsGameplayRunning));
                _coordinator = new MatchCoordinator(session, controlUnitHealth,
                    () => HasCurrentMatch && (network == null || network.IsGameplayRunning), _matchTime.Clock);
                // Feature의 최초 날씨 선택 이전에 주입합니다. Client에는 난수 결정권을 제공하지 않습니다.
                session.InitializeRandom(() => HasCurrentMatch && (network == null || _networkBinding.Connection.IsServer));
                // 영속 Encounter는 DDOL Scene에 있어 Scene 조회에서 빠지므로 동일 수명에 명시적으로 포함합니다.
                _encounterConsumer = ResolveEncounterConsumer();
                var randomConsumers = FindSceneBehaviours().OfType<IRandomConsumer>().ToList();
                if (_encounterConsumer is IRandomConsumer encounterRandom) randomConsumers.Add(encounterRandom);
                _randomConsumers = new RandomConsumerScope(session.Random, randomConsumers);
                _environmentScope = new MatchEnvironmentSceneScope(session, _coordinator, _weatherSource, _timeOfDaySource,
                    () => HasCurrentMatch && session.IsGameplayRunning && (network == null || network.IsGameplayRunning), _matchTime.Scheduler, _matchTime.Clock);
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
                _regionBinding.BindCombatSources(FindSceneBehaviours().OfType<IEnemyCombatSource>());
                if (_encounterConsumer != null)
                {
                    // 새 환경·추가 스폰 계약은 Feature가 구현한 경우에만 연결합니다.
                    // 현행 main consumer의 기존 Battlefield 스폰 경로는 계속 사용할 수 있습니다.
                    if (_encounterConsumer is IEncounterEnvironmentConsumer environmentConsumer)
                        environmentConsumer.BindEnvironment(matchId, () => HasCurrentMatch && _matchSession != null &&
                            _matchSession.IsGameplayRunning ? _matchSession.Environment?.Snapshot : null);
                    if (_encounterConsumer is IEncounterSpawnContextConsumer spawnConsumer)
                        spawnConsumer.BindSpawnContext(matchId, session.SpawnSources, _matchTime.Clock, config.RandomSeed, _coordinator.GameplayCancellation);
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
                (_environmentScope as MatchEnvironmentSceneScope)?.PublishCurrent();
                var fieldFeatures = GetFieldSimulationConsumers();
                _fieldScope = new MatchFieldSceneScope(config, _matchTime, _coordinator.GameplayCancellation,
                    () => HasCurrentMatch && session.IsGameplayRunning && (network == null || network.IsGameplayRunning),
                    fieldFeatures, _playerActors.Actors);
                BindCheckpoints();
                if (appMatchHost is AppRoot restoringApp) restoringApp.ApplyCheckpointArrival(this);
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
                .Where(component => component != null && component.isActiveAndEnabled).OfType<IControlUnitVitals>().ToArray();
            if (health.Length != 1 || health[0].MaxHealth <= 0)
                throw new InvalidOperationException("Provide one configured, active CU vitals source before Match preparation.");
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
        private List<IFieldSimulationConsumer> GetFieldSimulationConsumers()
        {
            var features = FindSceneBehaviours().OfType<IFieldSimulationConsumer>().ToList();
            var encounter = _encounterConsumer as IFieldSimulationConsumer ?? _encounterConsumerBehaviour as IFieldSimulationConsumer;
            if (encounter != null && !features.Contains(encounter)) features.Add(encounter);
            return features;
        }

        private void PrepareEnvironmentSources()
        {
            var weather = FindSceneBehaviours().OfType<IMatchWeatherSource>().ToArray();
            // Weather 옆에 공용 낮밤 source를 한 번 확보합니다. 실제 날씨 선택/연출은 소유하지 않습니다.
            if (weather.Length == 1 && !FindSceneBehaviours().OfType<IMatchTimeOfDaySource>().Any() && weather[0] is MonoBehaviour weatherComponent)
                weatherComponent.gameObject.AddComponent<MatchDayNightCycle>();
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
            MatchFieldSceneScope field = _fieldScope;
            MatchFieldReplicaScope replicaField = _replicaField;
            IDisposable environment = _environmentScope;
            RandomConsumerScope randomConsumers = _randomConsumers;
            IBattlefieldTurretInputSource input = _turretInputSource;
            IAppMatchHost host = _matchHost;
            bool ownedMatch = _ownsMatch;
            // 콜백의 실패/재진입과 관계없이 이전 Scene 소유권은 먼저 닫습니다.
            _matchSession = null;
            _coordinator = null;
            _matchTime = null; _timeReplica = null;
            _fieldScope = null; _replicaField = null;
            _environmentScope = null; _environmentReplica = null;
            _randomConsumers = null;
            _playerActors = null; _regionBinding = null; _turretBinding = null; _turretInputSource = null;
            _ownsMatch = false;
            _ownedMatchId = default;
            _enemyPositionSources = Array.Empty<IBattlefieldEnemyPositionSource>();
            _lastEntityCells.Clear();
            var errors = new List<Exception>();
            RunCleanup(ReleaseCheckpoints, errors);
            RunCleanup(() => coordinator?.Dispose(), errors);
            RunCleanup(() => environment?.Dispose(), errors);
            RunCleanup(() => randomConsumers?.Dispose(), errors);
            RunCleanup(() => ReleaseLocalActorConsumers(matchId), errors);
            RunCleanup(() => ReleaseEncounterConsumer(matchId), errors);
            RunCleanup(() => field?.Dispose(), errors);
            RunCleanup(() => replicaField?.Dispose(), errors);
            RunCleanup(() => FindSceneCameraFollow()?.Bind(null), errors);
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

        private void Update()
        {
            _matchTime?.Pump(Time.timeScale);
            if (_restoringCheckpoint) return;
            // main의 기존 CU 읽기 Port를 사용합니다. Feature가 새 파괴 이벤트 계약을 구현할 필요는 없습니다.
            _coordinator?.EvaluateProgress();
            if (!HasCurrentMatch || _matchSession == null || _playerActors == null || Time.unscaledTime < _nextBattlefieldSampleTime)
                return;
            if (!_matchSession.IsGameplayRunning) return;

            _nextBattlefieldSampleTime = Time.unscaledTime + BattlefieldSampleInterval;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { using (SpatialMarker.Auto()) RefreshDynamicInput(); }
            finally { LastSpatialRefreshMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency; }
        }
    }
}

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
                _turretBinding?.BindMatch(matchId, () => HasCurrentMatch && (network == null || network.Phase == NetworkMatchPhase.Running));
                MatchSession session = appMatchHost.StartMatch(
                    config,
                    initialState,
                    new InitialStagePocModeRules(),
                    turretBattlefieldInput,
                    dynamicBattlefieldInput,
                    grid);
                _matchHost = appMatchHost;
                _ownsMatch = true;
                _ownedMatchId = matchId;
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
                RegionSceneBinding[] regionBindings = FindObjectsByType<RegionSceneBinding>(FindObjectsSortMode.None)
                    .Where(binding => binding.gameObject.scene == gameObject.scene && binding.isActiveAndEnabled).ToArray();
                if (regionBindings.Length > 1) throw new InvalidOperationException("Keep one RegionSceneBinding per Match scene.");
                _regionBinding = regionBindings.FirstOrDefault();
                if (network == null) _regionBinding?.BindMatch(session, entityIds: _turretBinding);
                else
                {
                    if (_regionBinding == null) throw new InvalidOperationException("Network Match requires a RegionSceneBinding.");
                    _regionBinding.BindMatch(session, network.Descriptor.RegionGrid,
                        network.Descriptor.RegionMode, () => network.Phase == NetworkMatchPhase.Running, _turretBinding);
                }
                _encounterConsumer = ResolveEncounterConsumer();
                if (_encounterConsumer != null)
                {
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
            catch
            {
                ReleaseLocalActorConsumers(matchId);
                ReleaseEncounterConsumer(matchId);
                _regionBinding?.UnbindMatch();
                _regionBinding = null;
                _turretBinding?.UnbindMatch(matchId); _turretBinding = null;
                if (_turretInputSource != null)
                    _turretInputSource.LayoutChanged -= OnTurretLayoutChanged;
                _turretInputSource = null;
                _matchSession = null;
                _lastEntityCells.Clear();
                _playerActors?.Dispose();
                _playerActors = null;
                if (_ownsMatch)
                {
                    _matchHost.TryEndMatch(_ownedMatchId);
                    _ownsMatch = false;
                    _ownedMatchId = default;
                }
                throw;
            }
        }

        public RegionSceneBinding RegionBinding => GetComponent<RegionSceneBinding>();

        public void SetNetworkEncounterConsumer(MonoBehaviour consumer)
        {
            if (_ownsMatch || _playerActors != null) throw new InvalidOperationException("Set the consumer before Match composition.");
            if (consumer == null || !consumer.isActiveAndEnabled || !(consumer is IAppOwnedEncounterMatchConsumer))
                throw new ArgumentException("An active App-owned Encounter consumer is required.", nameof(consumer));
            _encounterConsumerBehaviour = consumer;
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
            PlayerStart[] starts = FindPlayerStarts();
            for (int slot = 0; slot < descriptor.Config.Players.Count; slot++)
                if (starts.Count(start => start.SlotIndex == slot) != 1)
                    throw new InvalidOperationException($"Expected exactly one active PlayerStart for slot {slot}.");
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
                FindSceneCameraFollow().Bind(_playerActors.Actors[playerId].transform);
            }
            catch { ReleaseNetworkMatch(_ownedMatchId); throw; }
        }

        public void BindRegionReplica(IRegionGridSource source)
        {
            _regionBinding = RegionBinding;
            _regionBinding.UnbindMatch();
            if (source != null) _regionBinding.BindReplica(source);
        }

        public void ReleaseNetworkMatch(MatchId matchId)
        {
            if (_ownedMatchId != matchId) return;
            if (_ownsMatch) { _matchHost.TryEndMatch(matchId); return; }
            ReleaseLocalActorConsumers(matchId);
            try { _playerActors?.Dispose(); }
            finally
            {
                _playerActors = null; _ownedMatchId = default;
                _regionBinding?.UnbindMatch(); _regionBinding = null;
                _turretBinding?.UnbindMatch(matchId); _turretBinding = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseLocalActorConsumers(_ownedMatchId);
            if (_matchSession != null) _matchSession.Disposing -= ReleaseMatchBindings;
            try
            {
                _playerActors?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                if (_matchSession != null)
                    ReleaseEncounterConsumer(_matchSession.Config.MatchId);
                _regionBinding?.UnbindMatch();
                _regionBinding = null;
                _turretBinding?.UnbindMatch(_ownedMatchId); _turretBinding = null;
                if (_turretInputSource != null)
                    _turretInputSource.LayoutChanged -= OnTurretLayoutChanged;
                _turretInputSource = null;
                _matchSession = null;
                _enemyPositionSources = Array.Empty<IBattlefieldEnemyPositionSource>();
                _lastEntityCells.Clear();
                _playerActors = null;
                if (_ownsMatch) _matchHost?.TryEndMatch(_ownedMatchId);
                _ownsMatch = false;
                _ownedMatchId = default;
                _matchHost = null;
            }
        }

        private void ReleaseMatchBindings()
        {
            MatchSession session = _matchSession;
            if (session == null) return;
            session.Disposing -= ReleaseMatchBindings;
            _matchSession = null;
            _ownsMatch = false;
            _ownedMatchId = default;
            ReleaseLocalActorConsumers(session.Config.MatchId);
            ReleaseEncounterConsumer(session.Config.MatchId);
            try { _playerActors?.Dispose(); }
            finally
            {
                _playerActors = null;
                _regionBinding?.UnbindMatch();
                _regionBinding = null;
                _turretBinding?.UnbindMatch(session.Config.MatchId); _turretBinding = null;
                if (_turretInputSource != null) _turretInputSource.LayoutChanged -= OnTurretLayoutChanged;
                _turretInputSource = null;
                _enemyPositionSources = Array.Empty<IBattlefieldEnemyPositionSource>();
                _lastEntityCells.Clear();
                _networkBinding?.OnAuthorityMatchReleased(session.Config.MatchId);
            }
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
            try { consumer?.UnbindMatch(matchId); }
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
            _matchHost.UpdateBattlefieldTurretLayout(_turretInputSource.CaptureTurretLayout());
        }

        private IBattlefieldEnemyPositionSource[] FindEnemyPositionSources()
        {
            return FindSceneBehaviours()
                .OfType<IBattlefieldEnemyPositionSource>()
                .ToArray();
        }

        private void Update()
        {
            if (!HasCurrentMatch || _matchSession == null || _playerActors == null || Time.unscaledTime < _nextBattlefieldSampleTime)
                return;

            _nextBattlefieldSampleTime = Time.unscaledTime + BattlefieldSampleInterval;
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

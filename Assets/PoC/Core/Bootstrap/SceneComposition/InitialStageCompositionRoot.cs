// Initial_Stage PoC에서 App이 제공한 Match 기능과 Scene 전용 Player actor를 조립합니다.
// 게임 규칙은 Core에, Unity 오브젝트 생성·정리는 이 Scene 범위 조립자가 맡습니다.

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

        private IAppMatchHost _matchHost;
        private MatchSession _matchSession;
        private MatchPlayerActorScope _playerActors;
        private IBattlefieldTurretInputSource _turretInputSource;
        private IBattlefieldEnemyPositionSource[] _enemyPositionSources = Array.Empty<IBattlefieldEnemyPositionSource>();
        private readonly Dictionary<TeamHJD.Game.Domain.EntityId, int> _lastEntityCells =
            new Dictionary<TeamHJD.Game.Domain.EntityId, int>();
        private float _nextBattlefieldSampleTime;
        private bool _ownsMatch;

        public void Compose(IAppMatchHost appMatchHost)
        {
            if (_matchHost != null) throw new InvalidOperationException("Initial Stage has already been composed.");
            if (appMatchHost == null) throw new ArgumentNullException(nameof(appMatchHost));
            if (playerPrefab == null) throw new InvalidOperationException("Assign the Stage_1 Player prefab before entering Play Mode.");
            if (battlefieldGridSettings == null) throw new InvalidOperationException("Assign shared BattlefieldGridSettings before entering Play Mode.");

            PlayerStart[] starts = FindPlayerStarts();
            if (starts.Length == 0) throw new InvalidOperationException("Initial_Stage requires at least one active PlayerStart.");
            MatchCameraFollow cameraFollow = FindSceneCameraFollow();
            if (cameraFollow == null) throw new InvalidOperationException("Initial_Stage requires a MatchCameraFollow on its Main Camera.");

            MatchId matchId = new MatchId(Guid.NewGuid().ToString("N"));
            var loadout = new PlayerLoadoutSnapshot(LocalPlayerId, new Dictionary<DefinitionId, int>());
            var config = new MatchConfig(
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
                new[] { new PlayerState(LocalPlayerId, 400, 400, true) },
                new ControlUnitState(1500, 1500, 250, 250),
                new WaveState(0, 1, 0, 0),
                Array.Empty<TurretState>(),
                Array.Empty<EnemyState>(),
                Array.Empty<SectorState>());

            PlayerStart firstStart = starts.OrderBy(start => start.SlotIndex).First();
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
                        new TeamHJD.Game.Domain.EntityId(LocalPlayerId.Value),
                        new BattlefieldPoint(firstStart.Position.x, firstStart.Position.y))
                },
                Array.Empty<EnemySpatialInput>());
            BattlefieldGridConfiguration grid = battlefieldGridSettings.CreateConfiguration();

            try
            {
                MatchSession session = appMatchHost.StartMatch(
                    config,
                    initialState,
                    new InitialStagePocModeRules(),
                    turretBattlefieldInput,
                    dynamicBattlefieldInput,
                    grid);
                _matchHost = appMatchHost;
                _ownsMatch = true;
                _turretInputSource = turretInputSource;
                if (_turretInputSource != null)
                    _turretInputSource.LayoutChanged += OnTurretLayoutChanged;
                _playerActors = MatchPlayerActorScope.SpawnPlayers(
                    session,
                    new LocalPlayerActorSpawner(playerPrefab, actorParent),
                    starts);
                _matchSession = session;
                _lastEntityCells.Clear();
                _nextBattlefieldSampleTime = 0f;
                cameraFollow.Bind(_playerActors.Actors[LocalPlayerId].transform);
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
                if (_turretInputSource != null)
                    _turretInputSource.LayoutChanged -= OnTurretLayoutChanged;
                _turretInputSource = null;
                _matchSession = null;
                _lastEntityCells.Clear();
                _playerActors?.Dispose();
                _playerActors = null;
                if (_ownsMatch)
                {
                    _matchHost.EndCurrentMatch();
                    _ownsMatch = false;
                    _matchHost = null;
                }
                throw;
            }
        }

        private void OnDestroy()
        {
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
                if (_turretInputSource != null)
                    _turretInputSource.LayoutChanged -= OnTurretLayoutChanged;
                _turretInputSource = null;
                _matchSession = null;
                _enemyPositionSources = Array.Empty<IBattlefieldEnemyPositionSource>();
                _lastEntityCells.Clear();
                _playerActors = null;
                if (_ownsMatch) _matchHost?.EndCurrentMatch();
                _ownsMatch = false;
                _matchHost = null;
            }
        }

        private PlayerStart[] FindPlayerStarts()
        {
            return FindObjectsByType<PlayerStart>(FindObjectsSortMode.None)
                .Where(start => start.gameObject.scene == gameObject.scene && start.isActiveAndEnabled)
                .ToArray();
        }

        private MatchCameraFollow FindSceneCameraFollow()
        {
            return FindObjectsByType<MatchCameraFollow>(FindObjectsSortMode.None)
                .FirstOrDefault(camera => camera.gameObject.scene == gameObject.scene);
        }

        private IBattlefieldTurretInputSource FindTurretInputSource()
        {
            MonoBehaviour source = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .FirstOrDefault(component =>
                    component.gameObject.scene == gameObject.scene &&
                    component is IBattlefieldTurretInputSource);
            return source as IBattlefieldTurretInputSource;
        }

        private void OnTurretLayoutChanged()
        {
            if (_matchHost == null || _turretInputSource == null) return;
            _matchHost.UpdateBattlefieldTurretLayout(_turretInputSource.CaptureTurretLayout());
        }

        private IBattlefieldEnemyPositionSource[] FindEnemyPositionSources()
        {
            return FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(component => component.gameObject.scene == gameObject.scene)
                .OfType<IBattlefieldEnemyPositionSource>()
                .ToArray();
        }

        private void Update()
        {
            if (_matchSession == null || _playerActors == null || Time.unscaledTime < _nextBattlefieldSampleTime)
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

            _matchSession.UpdateBattlefieldParticipants(nextInput);
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

// Initial_Stage PoC에서 App이 제공한 Match 기능과 Scene 전용 Player actor를 조립합니다.
// 게임 규칙은 Core에, Unity 오브젝트 생성·정리는 이 Scene 범위 조립자가 맡습니다.

using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Content.Authoring;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class InitialStageCompositionRoot : MonoBehaviour, ISceneCompositionRoot
    {
        private static readonly PlayerId LocalPlayerId = new PlayerId("local-player");

        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform actorParent;
        [SerializeField] private BattlefieldGridSettings battlefieldGridSettings;

        private IAppMatchHost _matchHost;
        private MatchPlayerActorScope _playerActors;
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
            var battlefieldInput = new BattlefieldDynamicSpatialInput(
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
                    BattlefieldSpatialInput.Empty,
                    battlefieldInput,
                    grid);
                _matchHost = appMatchHost;
                _ownsMatch = true;
                _playerActors = MatchPlayerActorScope.SpawnPlayers(
                    session,
                    new LocalPlayerActorSpawner(playerPrefab, actorParent),
                    starts);
                cameraFollow.Bind(_playerActors.Actors[LocalPlayerId].transform);
                Debug.Log($"Initial_Stage Match started ({matchId}); spawned {_playerActors.Actors.Count} Player actor(s).", this);
            }
            catch
            {
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
    }
}

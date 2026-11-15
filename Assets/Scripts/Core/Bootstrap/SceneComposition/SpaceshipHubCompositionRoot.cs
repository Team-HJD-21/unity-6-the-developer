// Startup에서 들어온 Match 없는 우주선의 Player·카메라를 생성하고 Scene 수명으로 정리합니다.
// 진행 중 필드의 Additive 우주선 방문에서는 기존 actor/카메라를 유지합니다.
// 개인 성장 저장은 기존 원본에 연결하며 전투 Match·날씨·NGO 권위를 만들지 않습니다.
using System;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class SpaceshipHubCompositionRoot : MonoBehaviour, ISceneCompositionRoot
    {
        private LocalPlayerActorSpawner _spawner;
        private MatchCameraFollow _follow;
        private GrowthSaveService _personalGrowth;
        private bool _reportedGrowthError;
        public GameObject Player { get; private set; }
        public Camera Camera { get; private set; }

        public void Compose(IAppMatchHost host)
        {
            if (!(host is AppRoot app))
                throw new InvalidOperationException("The starting hub requires App services.");
            if (!app.CurrentMatchId.IsEmpty || app.NetworkMatches?.Descriptor != null)
                return;
            if (_spawner != null)
                throw new InvalidOperationException("The starting hub is already composed.");
            var settings = Resources.Load<SpaceshipHubSettings>(SpaceshipHubSettings.ResourceKey);
            if (settings == null || settings.playerPrefab == null)
                throw new InvalidOperationException("Assign the starting hub Player prefab.");
            var markers = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Where(item => item.name == settings.spawnMarker).ToArray();
            if (markers.Length != 1)
                throw new InvalidOperationException("The starting hub requires one explicit spawn marker: " + settings.spawnMarker);
            try
            {
                var cameraObject = new GameObject("Hub Main Camera");
                cameraObject.transform.SetParent(transform);
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = markers[0].position + Vector3.back * 10;
                Camera = cameraObject.AddComponent<Camera>();
                Camera.orthographic = true;
                Camera.orthographicSize = settings.orthographicSize;
                Camera.cullingMask = settings.cameraCullingMask;
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = Color.clear;
                Camera.depth = -1;
                cameraObject.AddComponent<AudioListener>();
                _follow = cameraObject.AddComponent<MatchCameraFollow>();
                var startObject = new GameObject("Hub PlayerStart");
                startObject.transform.SetParent(transform);
                startObject.transform.position = markers[0].position;
                var start = startObject.AddComponent<PlayerStart>();
                _spawner = new LocalPlayerActorSpawner(settings.playerPrefab, transform, app.PresentationRandom);
                var playerId = InitialStageCompositionRoot.LocalPlayerId;
                Player = _spawner.Spawn(playerId, start);
                BindPersonalGrowth(app.Services.CheckpointStore, app.Services.CheckpointPayloads, playerId);
                foreach (var component in Player.GetComponents<MonoBehaviour>())
                {
                    if (component is ILocalInputConsumer input)
                        input.BindInputBlocker(app.Services.InputLocks);
                    if (component is IHubPlayerBinding hub)
                        hub.BindHub(app.Services.InputLocks);
                }

                _follow.Bind(Player.transform);
            }
            catch
            {
                Release();
                throw;
            }
        }

        private void BindPersonalGrowth(ICheckpointStore store, ICheckpointPayloadCodec codec, PlayerId playerId)
        {
            if (store == null || codec == null) return;
            var owners = Player.GetComponentsInChildren<MonoBehaviour>(true).OfType<IGrowthCheckpointParticipant>().ToArray();
            if (owners.Length == 0) return;
            // Match를 시작하지 않고 기존 개인 성장 section의 소유자/설정만 검증합니다.
            var config = new MatchConfig(new MatchId("hub-growth"), MatchMode.Story, new StageId("spaceship-hub"),
                new DefinitionId("poc-default-difficulty"), "poc", 1, new[] { new PlayerLoadoutSnapshot(playerId, null) });
            var context = new CheckpointCaptureContext(config, 0, Guid.NewGuid());
            var growth = new GrowthSaveService(store, codec, GrowthSaveService.SlotForOwner("player/" + playerId.Value), () => context, owners);
            try { growth.Load(); _personalGrowth = growth; }
            catch { growth.Dispose(); throw; }
        }

        private void Update()
        {
            if (_personalGrowth == null) return;
            _personalGrowth.Pump(Time.realtimeSinceStartupAsDouble);
            if (_personalGrowth.LastError != null && !_reportedGrowthError)
            {
                _reportedGrowthError = true;
                Debug.LogError("우주선 개인 성장 저장 실패: " + _personalGrowth.LastError.Message + ". 기존 저장은 보존했습니다.", this);
            }
            else if (_personalGrowth.LastError == null) _reportedGrowthError = false;
        }

        private void Release()
        {
            var growth = _personalGrowth; _personalGrowth = null;
            // actor를 없애기 전에 대기 중인 개인 성장 저장을 마칩니다.
            try { growth?.Dispose(); }
            finally { ReleaseActor(); }
        }

        private void ReleaseActor()
        {
            if (Player != null)
            {
                Player.SetActive(false);
                foreach (var component in Player.GetComponents<MonoBehaviour>())
                    if (component is IHubPlayerBinding hub)
                        hub.UnbindHub();
            }

            _follow?.Bind(null);
            _spawner?.Dispose();
            _spawner = null;
            Player = null;
        }

        private void OnDestroy() => Release();
    }
}

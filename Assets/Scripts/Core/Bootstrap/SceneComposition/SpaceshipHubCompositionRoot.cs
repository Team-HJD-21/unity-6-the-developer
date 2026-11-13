// Startup에서 들어온 Match 없는 우주선의 Player·카메라를 생성하고 Scene 수명으로 정리합니다.
// 진행 중 필드의 Additive 우주선 방문에서는 기존 actor/카메라를 유지합니다.
// 실제 전투·저장·날씨·NGO 권위를 만들지 않습니다.
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
                Player = _spawner.Spawn(new PlayerId("hub-local-player"), start);
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

        private void Release()
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

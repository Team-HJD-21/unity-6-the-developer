// Hub의 Player/카메라 생성은 실제 Awake/OnDestroy 수명에서 검사합니다.
// 각 케이스는 Play를 한 번만 진입/종료하며 도메인 재로드 이후 App 참조와 작업을 얻습니다.
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class AppHubArrivalRuntimeTests
    {
        [UnityTest]
        public IEnumerator DirectHubArrivalCreatesPlayerAndCameraWithoutCreatingGameFlow()
        {
            EditorSceneManager.OpenScene(AppScreenPresenter.HubScene);
            yield return new EnterPlayMode();
            yield return VerifyDirectHub();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyDirectHub()
        {
            yield return null;
            var app = Object.FindAnyObjectByType<AppRoot>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.IsInitialized, Is.True);
            Assert.That(app.GameFlow, Is.Null, "직접 실행의 조립을 위해 GameFlow를 임의 생성하지 않습니다.");
            AssertHub(app);
        }

        [UnityTest]
        public IEnumerator StartupHubArrivalKeepsTheExistingConfiguredGameFlow()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap/Startup.unity");
            yield return new EnterPlayMode();
            yield return VerifyStartupHub();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyStartupHub()
        {
            float deadline = Time.realtimeSinceStartup + 20;
            AppRoot app = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                app = Object.FindAnyObjectByType<AppRoot>();
                if (app != null && app.GameFlow?.Phase == GameFlowPhase.MainMenu) break;
                yield return null;
            }
            Assert.That(app?.GameFlow?.Phase, Is.EqualTo(GameFlowPhase.MainMenu));
            var flow = app.GameFlow;
            var travel = flow.EnterSpaceshipAsync();
            deadline = Time.realtimeSinceStartup + 40;
            while (!travel.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(travel.IsCompleted, Is.True, "Hub Scene 이동 시간이 초과됐습니다.");
            Assert.That(travel.IsFaulted || travel.IsCanceled, Is.False, travel.Exception?.ToString());
            Assert.That(app.GameFlow, Is.SameAs(flow));
            Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.Spaceship));
            AssertHub(app);
        }

        private static void AssertHub(AppRoot app)
        {
            var scene = SceneManager.GetSceneByPath(AppScreenPresenter.HubScene);
            Assert.That(scene.isLoaded, Is.True);
            var hub = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<SpaceshipHubCompositionRoot>()).Single();
            Assert.That(hub.Player, Is.Not.Null);
            Assert.That(hub.Camera, Is.Not.Null);
            Assert.That(hub.Player.scene, Is.EqualTo(scene));
            Assert.That(hub.Camera.gameObject.scene, Is.EqualTo(scene));
            Assert.That(hub.Camera.isActiveAndEnabled, Is.True);
            var follow = hub.Camera.GetComponent<MatchCameraFollow>();
            Assert.That(follow.Manager, Is.Not.Null, "실제 Awake에서 카메라 관리자가 연결돼야 합니다.");
            Assert.That(follow.Target, Is.SameAs(hub.Player.transform));
            typeof(AppRoot).GetMethod("ComposeScene", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { scene });
            Assert.That(scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<SpaceshipHubCompositionRoot>()).Count(), Is.EqualTo(1));
            Assert.That(app.CurrentMatchId.IsEmpty, Is.True);
        }

        [UnityTearDown]
        public IEnumerator ExitIfFailed()
        { if (EditorApplication.isPlaying) yield return new ExitPlayMode(); }
    }
}

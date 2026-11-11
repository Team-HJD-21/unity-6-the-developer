// 실제 Scene에서 로컬 컷씬·게임 정지 중 블렌드·출력 유지와 Startup/허브 왕복을 검사합니다.
// 각 검사는 실제 Play 진입/종료를 한 번만 수행하며 제품 Scene을 저장하지 않습니다.
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class CameraManagerRuntimeTests
    {
        private const string Stage = "Assets/Scenes/Gameplay/Initial_Stage.unity";

        [UnityTest, Explicit("실제 Stage와 GPU 프레임에서 카메라 수명을 확인할 때 실행합니다.")]
        public IEnumerator InitialStageCutsceneKeepsOutputAndWeatherThenRestoresPlayer()
        {
            EditorSceneManager.OpenScene(Stage);
            yield return new EnterPlayMode();
            yield return VerifyStage();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyStage()
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while ((CameraManager.Current == null || CameraManager.Current.ActiveTarget == null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            var manager = CameraManager.Current;
            Assert.That(manager, Is.Not.Null);
            Camera output = manager.OutputCamera;
            Transform player = manager.ActiveTarget;
            Assert.That(player, Is.Not.Null);
            Assert.That(Camera.main, Is.SameAs(output));
            float size = output.orthographicSize;
            var weather = output.GetComponents<MonoBehaviour>()
                .Where(item => item.GetType().Name is "WeatherShaderApply" or "PlayerWeatherEffectBinding" or "DayNightShaderApply").ToArray();
            Assert.That(weather.Length, Is.EqualTo(3));
            AssertSingleListener();
            var marker = new GameObject("Core camera runtime view");
            marker.transform.position = player.position + Vector3.right * 12;
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                var lease = manager.PushView(marker.transform, .15f, size * .75f);
                try
                {
                    deadline = Time.realtimeSinceStartup + 5;
                    while (manager.IsBlending && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(manager.IsBlending, Is.False, "게임 시간 정지 중에도 로컬 블렌드는 끝납니다.");
                    Assert.That(Vector2.Distance(output.transform.position, marker.transform.position), Is.LessThan(.01f));
                    Assert.That(Camera.main, Is.SameAs(output));
                    Assert.That(manager.OutputCamera, Is.SameAs(output));
                    Assert.That(output.orthographicSize, Is.EqualTo(size * .75f).Within(.001));
                    foreach (var item in weather) Assert.That(item != null && item.gameObject == output.gameObject, Is.True);
                    AssertSingleListener();
                }
                finally { lease.Dispose(); }
                deadline = Time.realtimeSinceStartup + 5;
                while (manager.IsBlending && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(manager.IsBlending, Is.False);
                Assert.That(manager.ActiveTarget, Is.SameAs(player));
                Assert.That(Vector2.Distance(output.transform.position, player.position), Is.LessThan(.01f));
                Assert.That(output.orthographicSize, Is.EqualTo(size).Within(.001));
                TestContext.WriteLine($"Initial_Stage camera={output.GetInstanceID()}, Weather components={weather.Length}, same output=true, paused blend/restore=true.");
            }
            finally { Time.timeScale = previousScale; Object.Destroy(marker); }
        }

        [UnityTest, Explicit("Startup→우주선→Stage→허브의 실제 Scene 전환을 확인할 때 실행합니다.")]
        public IEnumerator StartupHubStageAndReturnKeepOneOwnedOutput()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap/Startup.unity");
            yield return new EnterPlayMode();
            yield return VerifyFlow();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyFlow()
        {
            float deadline = Time.realtimeSinceStartup + 20;
            AppRoot app = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                app = Object.FindAnyObjectByType<AppRoot>();
                if (app?.GameFlow?.Phase == GameFlowPhase.MainMenu) break;
                yield return null;
            }
            Assert.That(app?.GameFlow?.Phase, Is.EqualTo(GameFlowPhase.MainMenu));
            yield return Wait(app.GameFlow.EnterSpaceshipAsync());
            Assert.That(app.GameFlow.Phase, Is.EqualTo(GameFlowPhase.Spaceship));
            Assert.That(CameraManager.Current?.ActiveTarget, Is.Not.Null);
            AssertSingleListener();
            yield return Wait(app.GameFlow.LaunchMissionAsync(Stage));
            Assert.That(app.GameFlow.Phase, Is.EqualTo(GameFlowPhase.Mission));
            Assert.That(CameraManager.Current?.OutputCamera.gameObject.scene.path, Is.EqualTo(Stage));
            AssertSingleListener();
            yield return Wait(app.GameFlow.ReturnToSpaceshipAsync());
            Assert.That(app.GameFlow.Phase, Is.EqualTo(GameFlowPhase.Spaceship));
            Assert.That(CameraManager.Current?.ActiveTarget, Is.Not.Null);
            AssertSingleListener();
            TestContext.WriteLine("Actual Startup→Hub→Initial_Stage→Hub: one active listener, live local camera target at each arrival.");
        }

        private static IEnumerator Wait(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 40;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Scene travel timeout.");
            Assert.That(task.IsFaulted || task.IsCanceled, Is.False, task.Exception?.ToString());
        }

        private static void AssertSingleListener() => Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
            .Count(item => item.enabled && item.gameObject.activeInHierarchy), Is.EqualTo(1));

        [UnityTearDown]
        public IEnumerator ExitIfFailed()
        { if (EditorApplication.isPlaying) yield return new ExitPlayMode(); }
    }
}

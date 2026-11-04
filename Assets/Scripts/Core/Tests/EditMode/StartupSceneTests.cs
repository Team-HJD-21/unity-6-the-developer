// 신규 Core 시작 Scene의 레거시 제거와 실제 시작 버튼→우주선 도착을 확인합니다.
// 기존 Hub/Player 콘텐츠 완성이나 보류한 Mission 조립을 통과로 간주하지 않습니다.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Domain;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class StartupSceneTests
    {
        private SceneSetup[] _setup;
        [SetUp] public void Remember()
        { if (!UnityEngine.Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
            if (_setup != null && _setup.Length > 0 && _setup.All(item => !string.IsNullOrEmpty(item.path)))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        [Test] public void StartupVisualCloneContainsNoMissingOrFeatureScripts()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap/Startup.unity");
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Assert.That(components.All(component => component != null), Is.True);
            Assert.That(components.All(component => component.GetType().Assembly.GetName().Name.StartsWith("Unity") ||
                component.GetType().FullName == "TeamHJD.Game.Presentation.StartMenuPresenter" ||
                component.GetType().FullName == "TeamHJD.Game.Bootstrap.SceneComposition.StartupSceneRoot"), Is.True);
            Assert.That(scene.GetRootGameObjects().Any(root => root.name == "Canvas"), Is.True);
        }
        [UnityTest] public IEnumerator ActualStartButtonEntersSpaceshipWithoutStartingAMatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap/Startup.unity");
            yield return new EnterPlayMode(); yield return null;
            var app = Object.FindAnyObjectByType<AppRoot>();
            Assert.That(app.GameFlow, Is.Not.Null); Assert.That(app.GameFlow.Phase, Is.EqualTo(GameFlowPhase.MainMenu));
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(item => item.name == "Start");
            button.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 20;
            while (app.GameFlow.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(app.GameFlow.LastFailure, Is.Null);
            Assert.That(app.GameFlow.Phase, Is.EqualTo(GameFlowPhase.Spaceship));
            Assert.That(app.CurrentMatchId.IsEmpty, Is.True);
            Assert.That(app.GameFlow.CurrentSceneKey, Is.EqualTo("Assets/Scenes/Spaceship/SpaceShipGrayBox.unity"));
        }
    }
}

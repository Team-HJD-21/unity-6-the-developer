// Editor 임시 저장의 종료 삭제·지연 쓰기 차단과 빌드 저장 보존을 검사합니다.
// 실제 AppBootstrap으로 Play를 시작/종료하고 정식 전장 진입 목록도 확인합니다.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class PlayModeStorageTests
    {
        private string _testRoot;

        [SetUp]
        public void CreateTestDirectory()
        {
            Debug.Log("PLAYMODE_STORAGE_TEST " + TestContext.CurrentContext.Test.Name);
            _testRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.temporaryCachePath, "CoreStoreTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_testRoot);
        }

        [TearDown]
        public void RemoveTestDirectory()
        {
            string parent = Path.GetFullPath(Path.Combine(UnityEngine.Application.temporaryCachePath, "CoreStoreTests")) + Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(_testRoot).StartsWith(parent, StringComparison.OrdinalIgnoreCase), Is.True);
            if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, recursive: true);
        }

        [UnityTearDown]
        public IEnumerator StopPlayOnFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void TemporaryStoreClearsOnlyItsSavedFilesAndOwnTemporaryFiles()
        {
            string folder = Path.Combine(_testRoot, "editor");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "old.checkpoint"), "stale");
            File.WriteAllText(Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".tmp"), "interrupted");
            File.WriteAllText(Path.Combine(folder, "keep.txt"), "not a save");
            File.WriteAllText(Path.Combine(folder, ".other.tmp"), "not our temporary file");
            using (var store = new BinaryCheckpointStore(folder, deleteOnDispose: true))
            {
                Assert.That(File.Exists(Path.Combine(folder, "old.checkpoint")), Is.False);
                store.SaveAsync(new CheckpointSlot("current"), Data()).GetAwaiter().GetResult();
                Assert.That(store.LoadAsync(new CheckpointSlot("current")).GetAwaiter().GetResult(), Is.Not.Null);
            }
            Assert.That(Directory.GetFiles(folder, "*.checkpoint"), Is.Empty);
            Assert.That(File.Exists(Path.Combine(folder, "keep.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(folder, ".other.tmp")), Is.True);
            Assert.That(Directory.GetFiles(folder, ".*.tmp").Length, Is.EqualTo(1));
        }

        [Test]
        public void PermanentStoreKeepsTheSaveAfterDisposal()
        {
            var slot = new CheckpointSlot("permanent");
            using (var store = new BinaryCheckpointStore(_testRoot))
                store.SaveAsync(slot, Data()).GetAwaiter().GetResult();
            using var next = new BinaryCheckpointStore(_testRoot);
            Assert.That(next.LoadAsync(slot).GetAwaiter().GetResult(), Is.Not.Null);
        }

        [Test]
        public void QueuedWritesCannotRecreateFilesAfterTheStoreCloses()
        {
            using var store = new BinaryCheckpointStore(_testRoot, deleteOnDispose: true);
            var data = Data();
            Task[] writes = Enumerable.Range(0, 24).Select(i => store.SaveAsync(new CheckpointSlot("queued-" + i), data)).ToArray();
            store.Dispose();
            foreach (var write in writes)
                try { write.GetAwaiter().GetResult(); }
                catch (ObjectDisposedException) { /* 종료 후 대기 중인 쓰기만 거절합니다. */ }
            Assert.That(Directory.GetFiles(_testRoot, "*.checkpoint"), Is.Empty);
            Assert.Throws<ObjectDisposedException>(() => store.SaveAsync(new CheckpointSlot("late"), data).GetAwaiter().GetResult());
            Assert.That(Directory.GetFiles(_testRoot, "*.checkpoint"), Is.Empty);
        }

        [Test]
        public void ProductionBuildRoutesUseStageOneInsteadOfTheOldInitialStage()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.That(scenes[0], Is.EqualTo("Assets/Scenes/Bootstrap/Startup.unity"));
            Assert.That(scenes[1], Is.EqualTo("Assets/Scenes/Gameplay/Stage_1.unity"));
            Assert.That(scenes.Count(s => s == "Assets/Scenes/Gameplay/Stage_1.unity"), Is.EqualTo(1));
            Assert.That(scenes, Does.Not.Contain("Assets/Scenes/Gameplay/Initial_Stage.unity"));
            var flow = new UnitySceneFlow();
            Assert.That(flow.CanLoad("Assets/Scenes/Gameplay/Stage_1.unity"), Is.True);
            Assert.That(flow.CanLoad("Assets/Scenes/Gameplay/Initial_Stage.unity"), Is.False);
        }

        [UnityTest]
        public IEnumerator StoppingActualAppPlayDeletesItsEditorSave()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var app = UnityEngine.Object.FindAnyObjectByType<AppRoot>();
            Assert.That(app, Is.Not.Null);
            var services = (TeamHJD.Game.Application.AppServices)typeof(AppRoot)
                .GetProperty("Services", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
            var store = services.CheckpointStore as BinaryCheckpointStore;
            Assert.That(store, Is.Not.Null);
            string directory = (string)typeof(BinaryCheckpointStore).GetField("_directory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(store);
            Assert.That(directory, Does.Contain("EditorPlayMode"));
            Assert.That(directory, Is.Not.EqualTo(Path.Combine(UnityEngine.Application.persistentDataPath, "Checkpoints")));
            string file = Path.Combine(directory, "play-exit-proof.checkpoint");
            Task save = store.SaveAsync(new CheckpointSlot("play-exit-proof"), Data());
            double until = EditorApplication.timeSinceStartup + 10;
            while (!save.IsCompleted && EditorApplication.timeSinceStartup < until) yield return null;
            Assert.That(save.IsCompleted, Is.True);
            save.GetAwaiter().GetResult();
            Assert.That(File.Exists(file), Is.True);
            yield return new ExitPlayMode();
            Assert.That(File.Exists(file), Is.False, "Play 종료 뒤 성장/안전장치 저장이 남으면 안 됩니다.");
        }

        [Test]
        public void LegacyPlayStartOverrideMovesToStageOneWithoutChangingTheEditingScene()
        {
            var previous = EditorSceneManager.playModeStartScene;
            var editingScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            try
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Gameplay/Initial_Stage.unity");
                typeof(TeamHJD.Game.Editor.AppPlayModeShutdown).GetMethod("OnStateChanged", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { PlayModeStateChange.ExitingEditMode });
                Assert.That(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene), Is.EqualTo("Assets/Scenes/Gameplay/Stage_1.unity"));
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Is.EqualTo(editingScene));
            }
            finally { EditorSceneManager.playModeStartScene = previous; }
        }

        [UnityTest]
        public IEnumerator StartupHubExitLoadsStageOneWithFiveHundredPower()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap/Startup.unity");
            yield return new EnterPlayMode();
            var app = UnityEngine.Object.FindAnyObjectByType<AppRoot>();
            double until = EditorApplication.timeSinceStartup + 20;
            while (app?.GameFlow?.Phase != GameFlowPhase.MainMenu && EditorApplication.timeSinceStartup < until)
            {
                yield return null;
                app = UnityEngine.Object.FindAnyObjectByType<AppRoot>();
            }
            Assert.That(app?.GameFlow?.Phase, Is.EqualTo(GameFlowPhase.MainMenu));
            var hubTravel = app.GameFlow.EnterSpaceshipAsync();
            yield return WaitForTravel(hubTravel);
            var hubScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(AppRoot.DefaultHubScene);
            var objects = hubScene.GetRootGameObjects();
            var hub = objects.SelectMany(o => o.GetComponentsInChildren<SpaceshipHubCompositionRoot>()).Single();
            var exit = objects.SelectMany(o => o.GetComponentsInChildren<MonoBehaviour>())
                .Single(o => o != null && o.GetType().Name == "SpaceshipExit");
            var destination = (string)exit.GetType().GetField("missionSceneName", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(exit);
            Assert.That(destination, Is.EqualTo(AppRoot.DefaultStage1Scene));
            var playerCollider = hub.Player.GetComponentsInChildren<Collider2D>()
                .First(c => c.GetComponentInParent<PlayerActorIdentity>() != null);
            var exitTravel = (Task)exit.GetType().GetMethod("ExitAsync").Invoke(exit, new object[] { playerCollider });
            yield return WaitForTravel(exitTravel);
            Assert.That(app.GameFlow.CurrentSceneKey, Is.EqualTo(AppRoot.DefaultStage1Scene));
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Gameplay/Initial_Stage.unity").isLoaded, Is.False);
            var stage = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(AppRoot.DefaultStage1Scene);
            var controller = stage.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<TeamHJD.Game.FieldTurrets.FieldTurretController>()).Single();
            Assert.That(controller.MaximumPower, Is.EqualTo(500));
            Assert.That(app.CurrentMatchId.IsEmpty, Is.False);
            Debug.Log("PLAYMODE_STORAGE_ROUTE Stage_1 / maximumPower=500 / Match ready");
        }

        private static IEnumerator WaitForTravel(Task travel)
        {
            double until = EditorApplication.timeSinceStartup + 40;
            while (!travel.IsCompleted && EditorApplication.timeSinceStartup < until) yield return null;
            Assert.That(travel.IsCompleted, Is.True, "실제 Scene 이동이 제한 시간을 넘었습니다.");
            Assert.That(travel.IsFaulted || travel.IsCanceled, Is.False, travel.Exception?.ToString());
        }

        private static CheckpointData Data() => new(Guid.NewGuid(), DateTimeOffset.UtcNow, new MatchId("store-test"),
            new StageId("stage-1"), new DefinitionId("normal"), MatchMode.Story, 1, "v1", 0, 1,
            new[] { new CheckpointSection("test", 1, CheckpointSectionKind.Progression, new byte[] { 1 }) });
    }
}

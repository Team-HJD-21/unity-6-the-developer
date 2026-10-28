// 실제 Scene Single 재로드와 Hub 전환에서 새 Match·CU·Player·Executor 연결을 확인합니다.
// 로컬 Host 검사이며 실제 원격 Player/다중 참가자 Scene 동기화를 대체하지 않습니다.
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CoreSceneFlowIntegrationTests
{
    private const string Stage = "Assets/Scenes/Gameplay/Initial_Stage.unity";
    private const string Hub = "Assets/Scenes/Spaceship/SpaceShipGrayBox.unity";
    private SceneSetup[] _setup;
    [SetUp] public void RememberScenes() { if (!Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var app = Object.FindAnyObjectByType<AppRoot>(); if (app != null) app.EndCurrentMatch();
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (_setup != null && _setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_setup);
    }

    [UnityTest]
    public IEnumerator ActualReloadCreatesFreshMatchAndHubReturnDoesNotLeakActorsOrInputLocks()
    {
        EditorSceneManager.OpenScene(Stage);
        yield return new EnterPlayMode(); yield return null;
        var app = Object.FindAnyObjectByType<AppRoot>();
        var scene = Object.FindAnyObjectByType<InitialStageCompositionRoot>();
        Assert.That(scene.MatchCoordinator, Is.Not.Null);
        Assert.That(app.SceneTransitions, Is.Not.Null);
        var firstMatch = app.CurrentMatchId;
        var previousExecutors = Object.FindObjectsByType<EnemySpawnExecutor>(FindObjectsSortMode.None);
        int authoredExecutorCount = previousExecutors.Length;
        var configuration = scene.GetComponent<EncounterSceneConfiguration>();
        Assert.That(configuration, Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<EnemyNetworkTestLauncher>().ExecutionTarget, Is.SameAs(configuration.SpawnExecutor),
            "Production must use the authored executor rather than FindAnyObjectByType.");
        var player = Object.FindAnyObjectByType<PlayerControlState>();
        var menu = app.LocalInput.Acquire("test-menu");
        var popup = app.LocalInput.Acquire("test-popup");
        player.SetInputBlocked(true); menu.Dispose(); popup.Dispose();
        Assert.That(player.IsBlocked, Is.True, "Closing UI locks must not undo the Feature death/action lock.");
        player.SetInputBlocked(false);
        var cu = scene.RegionBinding.ControlUnit.GetComponent<ControlUnitHealth>();
        cu.ApplyDamage(cu.CurrentHealth);
        Assert.That(scene.MatchCoordinator.Completion, Is.Not.Null);
        yield return null;
        Assert.That(app.NetworkMatches.TryEnd(), Is.True, "The game flow ends the Match before Scene replacement.");
        Task transition = app.SceneTransitions.ReplaceAsync(Stage, CancellationToken.None);
        while (!transition.IsCompleted) yield return null;
        transition.GetAwaiter().GetResult();
        float deadline = Time.realtimeSinceStartup + 15;
        while (app.CurrentMatchId.IsEmpty && Time.realtimeSinceStartup < deadline) yield return null;
        scene = Object.FindAnyObjectByType<InitialStageCompositionRoot>();
        Assert.That(scene.MatchCoordinator, Is.Not.Null);
        Assert.That(app.CurrentMatchId, Is.Not.EqualTo(firstMatch));
        Assert.That(scene.RegionBinding.ControlUnit.GetComponent<ControlUnitHealth>().CurrentHealth, Is.GreaterThan(0));
        Assert.That(Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        // main에는 제작된 Executor 두 개가 있습니다. 개수 1을 강제하지 않고 이전 객체의 실제 폐기를 확인합니다.
        foreach (var previous in previousExecutors) Assert.That(previous == null, Is.True, "Previous Scene executors must be destroyed.");
        var currentExecutors = Object.FindObjectsByType<EnemySpawnExecutor>(FindObjectsSortMode.None);
        Assert.That(currentExecutors.Length, Is.EqualTo(authoredExecutorCount), DescribeExecutors());
        foreach (var executor in currentExecutors) Assert.That(executor.gameObject.scene.path, Is.EqualTo(Stage));
        Assert.That(Object.FindObjectsByType<Unity.Netcode.NetworkManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        var launcher = Object.FindAnyObjectByType<EnemyNetworkTestLauncher>();
        Assert.That(launcher.ExecutionTarget, Is.SameAs(scene.GetComponent<EncounterSceneConfiguration>().SpawnExecutor),
            "The persistent consumer must receive the reloaded Scene's authored executor.");
        deadline = Time.realtimeSinceStartup + 15;
        while (launcher.ActiveEnemyCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(launcher.ActiveEnemyCount, Is.GreaterThan(0), "Reload must bind the new Scene executor and SpawnPoints.");
        Assert.That(app.NetworkMatches.TryEnd(), Is.True);
        transition = app.SceneTransitions.ReplaceAsync(Hub, CancellationToken.None);
        while (!transition.IsCompleted) yield return null;
        transition.GetAwaiter().GetResult(); yield return null;
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Hub));
        Assert.That(app.CurrentMatchId.IsEmpty, Is.True);
        Assert.That(Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None).Length, Is.Zero);
        Assert.That(app.LocalInput.IsBlocked, Is.False);
        Assert.That(app.SceneTransitions.IsTransitioning, Is.False);
    }

    private static string DescribeExecutors()
    {
        var details = new System.Text.StringBuilder("Executor lifetime after NGO Single reload:\n");
        foreach (var executor in Object.FindObjectsByType<EnemySpawnExecutor>(FindObjectsSortMode.None))
        {
            var actor = executor.GetComponent<Unity.Netcode.NetworkObject>();
            details.AppendLine($"{executor.name}; scene={executor.gameObject.scene.name}; path={executor.gameObject.scene.path}; " +
                $"spawned={actor.IsSpawned}; sceneObject={actor.InScenePlaced}; destroyWithScene={actor.DestroyWithScene}");
        }
        return details.ToString();
    }
}

// 실제 Initial_Stage에서 App 소유 Host·준비·시작·적 정리·Match 재시작을 검사합니다.
// 두 실행 파일의 Client Scene 통합이나 전체 Feature 전투 복제를 대체하지 않습니다.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Editor;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class InitialStageNetworkSceneTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var root = Object.FindAnyObjectByType<AppRoot>();
        if (root != null) root.EndCurrentMatch();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator ExplicitHostStartDoesNotCreateMatch_AndMatchEndKeepsConnectionForRestart()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        NetworkSceneSetup.InstallOnActiveScene();
        SetManualEntry();
        yield return new EnterPlayMode();
        yield return null;
        var binding = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        var app = Object.FindAnyObjectByType<AppRoot>();
        Assert.That(binding.Connection.State, Is.EqualTo(NetworkConnectionState.Disconnected));
        Assert.That(app.CurrentMatchId.IsEmpty, Is.True);
        Assert.That(binding.TryStartHost(), Is.True);
        Assert.That(app.CurrentMatchId.IsEmpty, Is.True, "Opening a connection is not starting a Match.");
        Assert.That(binding.TryStartMatch(), Is.False);
        Assert.That(binding.TryPrepareMatch(), Is.True);
        Assert.That(app.CurrentMatchId.IsEmpty, Is.True);
        Assert.That(binding.TryStartMatch(), Is.True);
        Assert.That(app.NetworkRegions.Source, Is.Not.Null);
        Assert.That(app.CurrentMatchId, Is.EqualTo(binding.Matches.Descriptor.Config.MatchId));
        var first = app.CurrentMatchId;
        Assert.That(Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        var launcher = Object.FindAnyObjectByType<EnemyNetworkTestLauncher>();
        float deadline = Time.realtimeSinceStartup + 15;
        while (!launcher.RecentDiagnostics.Any(item => item.Kind == GameDiagnosticKind.RequestCompleted) &&
            Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(launcher.ActiveEnemyCount, Is.GreaterThan(0), "The App-owned Encounter must actually execute.");
        Assert.That(binding.TryEndMatch(), Is.True);
        Assert.That(app.CurrentMatchId.IsEmpty, Is.True);
        Assert.That(binding.Connection.State, Is.EqualTo(NetworkConnectionState.Hosting));
        Assert.That(launcher.ActiveEnemyCount, Is.Zero);
        Assert.That(Object.FindAnyObjectByType<EnemySpawnExecutor>().SpawnedEnemyCount, Is.Zero);
        yield return null;
        Assert.That(Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None).Length, Is.Zero);
        Assert.That(binding.TryPrepareMatch(), Is.True);
        Assert.That(binding.TryStartMatch(), Is.True);
        Assert.That(app.CurrentMatchId, Is.Not.EqualTo(first));
        Assert.That(Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(binding.TryEndMatch(), Is.True);
        yield return new ExitPlayMode();
    }
    private static void SetManualEntry()
    {
        var binding = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        var serialized = new SerializedObject(binding);
        serialized.FindProperty("_entry").enumValueIndex = (int)InitialStageNetworkEntry.Manual;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

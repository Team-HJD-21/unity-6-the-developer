// 실제 Initial_Stage에서 정식 요청·재전송·변조 요청과 생성 수를 대조합니다.
// 1인 Host 검증이며 원격 Client 전송을 대신하지 않습니다.
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
/// <summary>
/// 공통 Encounter 요청의 검증과 실행 결과 전달을 검사한다.
/// </summary>
public sealed class EncounterRequestIntegrationTests
{
    [UnityTearDown]
    public IEnumerator StopScene()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator ActualHost_ExecutesOnceAndRejectsChangedPayloadAndIssuer()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        var launcher = Object.FindAnyObjectByType<EncounterSceneBinding>();
        float deadline = Time.realtimeSinceStartup + 20;
        while (!launcher.RecentDiagnostics.Any(item => item.Kind == GameDiagnosticKind.RequestCompleted) &&
            Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(launcher.RecentDiagnostics.Any(item => item.Kind == GameDiagnosticKind.RequestCompleted), Is.True);
        var executor = Object.FindAnyObjectByType<EnemySpawnExecutor>();
        var issuer = new PlayerId("local-player");
        var context = new MatchRequestContext(launcher.BoundMatchId, Guid.NewGuid(), issuer);
        var request = new EncounterRequest(context, new DefinitionId("Normal"), 10, EncounterRequestSource.RuntimeUI);
        int before = executor.SpawnedEnemyCount;
        EncounterRequestResult result = launcher.Submit(request, issuer);
        Assert.That(result.Status, Is.EqualTo(EncounterRequestStatus.Spawned), result.Reason);
        Assert.That(result.SpawnedCount, Is.EqualTo(10));
        int after = executor.SpawnedEnemyCount;
        Assert.That(after - before, Is.EqualTo(10),
            $"before={before}, after={after}, bound={launcher.ActiveEnemyCount}, " +
            $"executors={Object.FindObjectsByType<EnemySpawnExecutor>(FindObjectsSortMode.None).Length}, " +
            $"spawnedIds={string.Join(",", Object.FindObjectsByType<EnemyNetworkController>(FindObjectsSortMode.None).Select(item => item.NetworkObjectId))}");
        var replay = launcher.Submit(request, issuer);
        Assert.That(replay.IsReplay, Is.True);
        Assert.That(executor.SpawnedEnemyCount, Is.EqualTo(after));
        Assert.That(launcher.Submit(new EncounterRequest(context, new DefinitionId("Normal"), 20,
            EncounterRequestSource.RuntimeUI), issuer).Status, Is.EqualTo(EncounterRequestStatus.RequestConflict));
        Assert.That(launcher.Submit(request, new PlayerId("forged")).Status, Is.EqualTo(EncounterRequestStatus.Unauthorized));
        Assert.That(executor.SpawnedEnemyCount, Is.EqualTo(after));
        launcher.UnbindMatch(context.MatchId);
        Assert.That(launcher.Submit(request, issuer).Status, Is.EqualTo(EncounterRequestStatus.MatchUnavailable));
        yield return new ExitPlayMode();
    }
}

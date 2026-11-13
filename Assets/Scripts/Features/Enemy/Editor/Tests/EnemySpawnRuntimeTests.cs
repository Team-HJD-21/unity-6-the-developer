// 실제 Initial_Stage의 반복 입장·스폰·Match 시간 등록과 진단 기록을 확인합니다.
// 성공 로그 제거가 반환 결과나 Game Debugger 자료를 지우지 않는지도 검사합니다.
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemySpawnRuntimeTests
{
    private const string PreviousMatchKey = "EnemySpawnRuntimeTests.PreviousMatch";

    [UnityTest]
    [Explicit("실제 Initial_Stage의 Encounter·시간 등록·진단을 검증할 때 실행합니다.")]
    public IEnumerator InitialStageSpawnKeepsBindingsDiagnosticsAndQuietSuccessLogs() => RunStage();

    [UnityTest]
    [Explicit("첫 실행 뒤 새 Play 세션에서도 생성/등록이 유지되는지 확인합니다.")]
    public IEnumerator InitialStageFreshSessionKeepsBindingsAndDiagnostics() => RunStage();

    private static IEnumerator RunStage()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        yield return new EnterPlayMode();
        // 테스트 하나에 Play 진입/종료는 한 번씩만 둡니다.
        yield return VerifyCurrentStage();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyCurrentStage()
    {
        EncounterSceneBinding launcher = null;
        float deadline = Time.realtimeSinceStartup + 25;
        while (Time.realtimeSinceStartup < deadline)
        {
            launcher = Object.FindAnyObjectByType<EncounterSceneBinding>();
            if (launcher != null && !launcher.BoundMatchId.IsEmpty && launcher.ExecutionTarget != null &&
                launcher.ExecutionTarget.CountForMatch(launcher.BoundMatchId) > 0) break;
            yield return null;
        }
        Assert.That(launcher, Is.Not.Null);
        Assert.That(launcher.ExecutionTarget, Is.Not.Null);
        Assert.That(launcher.ExecutionTarget.CountForMatch(launcher.BoundMatchId), Is.GreaterThan(0));
        string match = launcher.BoundMatchId.Value;
        Assert.That(match, Is.Not.EqualTo(SessionState.GetString(PreviousMatchKey, "")));
        SessionState.SetString(PreviousMatchKey, match);
        var messages = new List<string>();
        void Capture(string text, string stack, LogType kind) => messages.Add(text);
        Application.logMessageReceived += Capture;
        bool requested;
        string reason;
        try
        {
            requested = launcher.TryRequestCurrentEncounter("Normal", 10, EncounterRequestSource.DebugWindow, out reason);
            yield return null;
        }
        finally { Application.logMessageReceived -= Capture; }
        Assert.That(requested, Is.True, reason);
        Assert.That(messages.Exists(text => text.Contains("doesn't know its NetworkBehaviour yet") ||
            text.StartsWith("NavMesh 경로 발견:") || text.StartsWith("Match " + match + " r")), Is.False);
        int bound = 0, requests = 0, actuals = 0;
        foreach (var enemy in Object.FindObjectsByType<EnemyNetworkController>(FindObjectsSortMode.None))
        {
            if (!enemy.IsSpawned) continue;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(typeof(EnemyNetworkController).GetProperty("SimulationMatchId", flags).GetValue(enemy), Is.EqualTo(match));
            Assert.That(typeof(EnemyNetworkController).GetProperty("SimulationOwnerId", flags).GetValue(enemy),
                Is.EqualTo(launcher.ExecutionTarget.NetworkObjectId));
            var actor = enemy.GetComponent<TeamHJD.Game.Presentation.Scene.FieldSimulationActor>();
            Assert.That(actor, Is.Not.Null);
            Assert.That(actor.TimeSource, Is.Not.Null);
            Assert.That(actor.TimeSource.Snapshot.MatchId.Value, Is.EqualTo(match));
            bound++;
        }
        foreach (var item in launcher.RecentDiagnostics)
        {
            if (item.Kind == GameDiagnosticKind.RequestCompleted) requests++;
            if (item.SpawnDecision != null) actuals += item.SpawnDecision.Actuals.Count;
        }
        Assert.That(bound, Is.GreaterThan(0));
        Assert.That(requests, Is.GreaterThanOrEqualTo(2));
        Assert.That(actuals, Is.GreaterThan(0));
        TestContext.WriteLine($"Initial_Stage match={match}; bound enemies={bound}; completed requests={requests}; recorded actuals={actuals}; unwanted request logs=0.");
    }

    [UnityTearDown]
    public IEnumerator ExitIfFailed()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}

// Initial_Stage에서 디버그 버튼 없이 첫 Encounter가 생성되는지 확인합니다.
// NGO Host와 전체 Scene을 사용하는 명시적 실행 전용 통합 테스트입니다.

using System.Collections;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Editor;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class InitialStageEncounterIntegrationTests
{
    [UnityTest]
    [Explicit("Stage 1 통합 Scene과 NGO Host가 필요한 경우에만 실행")]
    public IEnumerator InitialStage_StartsEncounterWithoutDebugRequest()
    {
        EditorSceneManager.OpenScene("Assets/PoC/Spaceship/Scenes/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();

        EnemyNetworkTestLauncher launcher = Object.FindAnyObjectByType<EnemyNetworkTestLauncher>();
        Assert.That(launcher, Is.Not.Null, "Initial_Stage의 EnemyNetworkTestLauncher가 로드되어야 합니다.");

        float deadline = Time.realtimeSinceStartup + 20f;
        int spawnedEnemies = 0;
        bool serverStarted = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            EnemySpawnExecutor executor = Object.FindAnyObjectByType<EnemySpawnExecutor>();
            spawnedEnemies = executor != null ? executor.SpawnedEnemyCount : 0;
            serverStarted = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            if (spawnedEnemies > 0) break;
            yield return null;
        }

        bool recordedAuto = launcher.RecentDiagnostics.Any(item => item.Kind == GameDiagnosticKind.RequestCompleted && item.Source == EncounterRequestSource.MatchStart);
        int observedSpawned = launcher.RecentDiagnostics.Where(item => item.Kind == GameDiagnosticKind.EnemySpawned).Sum(item => item.ActualCount);
        SpawnerTester sceneTrigger = Object.FindAnyObjectByType<SpawnerTester>();
        Assert.That(sceneTrigger, Is.Not.Null);
        bool sceneRequest = sceneTrigger.TryRequestEncounter(out string sceneResult);
        bool uiRequest = launcher.TryRequestCurrentEncounter("Normal",10,EncounterRequestSource.RuntimeUI,out string uiResult);
        bool debugRequest = launcher.TryRequestCurrentEncounter("Normal",10,EncounterRequestSource.DebugWindow,out string debugResult);
        // 반복 UI와 같은 상태 머신이 최신 Match 요청 계약으로 실제 생성하는지도 함께 검사합니다.
        var repeat = new EncounterRepeatScheduler();
        string repeatMatch = launcher.BoundMatchId.ToString();
        int repeatBefore = launcher.RecentDiagnostics.Count(item => item.Kind == GameDiagnosticKind.EnemySpawned);
        repeat.Start(repeatMatch, 0.1, 2);
        repeat.Tick(0.1, repeatMatch, true, true, true, null, RequestRepeat);
        bool repeatPaused = repeat.CompletedRequests == 0;
        repeat.Tick(0.1, repeatMatch, true, false, true, null, RequestRepeat);
        repeat.Tick(0.1, repeatMatch, true, false, true, null, RequestRepeat);
        bool repeatSpawned = repeat.CompletedRequests == 2 && !repeat.IsRunning &&
            launcher.RecentDiagnostics.Count(item => item.Kind == GameDiagnosticKind.EnemySpawned) == repeatBefore + 20;
        var decision=launcher.RecentDiagnostics.LastOrDefault(item=>item.SpawnDecision!=null)?.SpawnDecision;
        bool capturedDecision=decision!=null&&decision.Candidates.Count>0&&decision.Plans.Count>0&&decision.Actuals.Count>0;
        int deathBefore=launcher.RecentDiagnostics.Count(item=>item.Kind==GameDiagnosticKind.EnemyDied);
        var health=Object.FindAnyObjectByType<EnemyHealth>();
        Assert.That(health,Is.Not.Null);
        health.TakeDamage(health.MaxHealth);
        yield return null;
        bool deathObserved=launcher.RecentDiagnostics.Count(item=>item.Kind==GameDiagnosticKind.EnemyDied)==deathBefore+1;
        yield return null;
        var root = Object.FindAnyObjectByType<TeamHJD.Game.Bootstrap.AppRoot>();
        int triangles = root == null ? 0 : root.CurrentBattlefieldSnapshot.Triangles.Count;
        int frontline = root == null ? 0 : root.CurrentBattlefieldSnapshot.FrontlineEdges.Count;
        string matchId = launcher.BoundMatchId.ToString();
        GameDebuggerSession.ShouldPauseOnFailure = true;
        GameDebuggerSession.Rearm();
        bool missingRejected = !launcher.TryRequestEncounter(null,"Normal",10,EncounterRequestSource.DebugWindow,out _);
        bool conditionalPause = UnityEditor.EditorApplication.isPaused;
        GameSimulationControls.Resume();
        GameDebuggerSession.ShouldPauseOnFailure = false;
        GameDebuggerSession.Rearm();
        var staleSnapshot = TeamHJD.Game.Domain.BattlefieldSpatialSnapshot.CreateMatchSnapshot(root.CurrentBattlefieldSnapshot,
            new TeamHJD.Game.Domain.MatchId("stale-match"), root.CurrentBattlefieldSnapshot.Revision, root.CurrentBattlefieldSnapshot.Grid);
        bool staleRejected = !launcher.TryRequestEncounter(staleSnapshot,"Normal",10,EncounterRequestSource.DebugWindow,out _);
        var monster = Object.FindAnyObjectByType<EnemyController>();
        Assert.That(monster, Is.Not.Null);
        monster.GetComponent<NetworkObject>().Despawn();
        yield return null;
        bool removalRecorded = launcher.RecentDiagnostics.Any(item => item.Kind == GameDiagnosticKind.EnemyRemoved);
        bool recorderConnected = GameDebuggerSession.Current != null && GameDebuggerSession.Current.Events.Any(item => item.Kind == GameDiagnosticKind.EnemySpawned);

        yield return new ExitPlayMode();
        Assert.That(serverStarted, Is.True, "Match 시작 뒤 NGO Host가 자동으로 실행되어야 합니다.");
        Assert.That(spawnedEnemies, Is.GreaterThan(0), "디버그 버튼 없이 첫 분대가 생성되어야 합니다.");
        Assert.That(recordedAuto, Is.True);
        Assert.That(observedSpawned, Is.GreaterThan(0));
        Assert.That(sceneRequest, Is.True, sceneResult);
        Assert.That(uiRequest, Is.True, uiResult);
        Assert.That(debugRequest, Is.True, debugResult);
        Assert.That(repeatPaused && repeatSpawned, Is.True, "반복 요청도 Pause를 존중하며 공통 Match 계약으로 2회/20마리를 생성해야 합니다.");
        Assert.That(capturedDecision,Is.True,"실제 판단 후보/계획/생성 위치가 요청 완료에 포함되어야 합니다.");
        Assert.That(deathObserved,Is.True,"EnemyHealth 확정 사망은 일반 Despawn과 별도로 정확히 1회 기록되어야 합니다.");
        Assert.That(triangles, Is.GreaterThan(0), "Scene 터렛 배치로 Core topology가 형성되어야 합니다.");
        Assert.That(frontline, Is.GreaterThan(0));
        Assert.That(missingRejected && staleRejected, Is.True, "빈 snapshot/이전 Match 요청은 공간 검사 우회 없이 거부해야 합니다.");
        Assert.That(conditionalPause, Is.True, "무장한 요청 실패 조건만 로컬 Editor를 정지해야 합니다.");
        Assert.That(removalRecorded, Is.True, "실제 Despawn은 처치와 구분된 제거 사실로 기록되어야 합니다.");
        Assert.That(recorderConnected, Is.True, "창을 열지 않아도 실제 생성 기록을 수집해야 합니다.");
        Assert.That(GameDebuggerSession.Recorder.Find(matchId), Is.Not.Null, "Play 종료/domain reload 이후에도 비교용 기록을 유지해야 합니다.");
        Assert.That(GameDebuggerSession.Recorder.Find(matchId).IsComplete, Is.True, "정상 Unbind 종료 기록이 있어야 합니다.");
    }

    // EnterPlayMode의 domain reload를 넘는 coroutine에 closure를 보관하지 않습니다.
    private static bool RequestRepeat(out string result)
    {
        var launcher = Object.FindAnyObjectByType<EnemyNetworkTestLauncher>();
        if (launcher == null) { result = "Encounter launcher missing"; return false; }
        return launcher.TryRequestCurrentEncounter("Normal", 10, EncounterRequestSource.DebugWindow, out result);
    }
}

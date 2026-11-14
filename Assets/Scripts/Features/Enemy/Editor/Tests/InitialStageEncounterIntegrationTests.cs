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

/// <summary>
/// Initial_Stage의 Encounter 시작과 생성·사망 기록 연결을 검사한다.
/// </summary>
public sealed class InitialStageEncounterIntegrationTests
{
    [UnityTest]
    [Explicit("Initial_Stage와 NGO Host에서 실제 지점/셀별 반복 배분을 검증")]
    public IEnumerator InitialStage_DistributesRepeatedRequestsAcrossAvailableGridCells()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        var launcher = Object.FindAnyObjectByType<EncounterSceneBinding>();
        Assert.That(launcher, Is.Not.Null);
        float deadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < deadline && !launcher.RecentDiagnostics.Any(e => e.SpawnDecision != null))
            yield return null;
        var root = Object.FindAnyObjectByType<TeamHJD.Game.Bootstrap.AppRoot>();
        Assert.That(root, Is.Not.Null);
        var snapshot = root.CurrentBattlefieldSnapshot;
        Assert.That(snapshot, Is.Not.Null);
        Assert.That(snapshot.FrontlineEdges, Is.Not.Empty, "실제 Scene 터렛의 전선 기하를 사용해야 합니다.");

        // 이동 후 비는 셀을 모사하기 위해 같은 snapshot을 유지합니다. 실제 NGO 생성/이력 commit은 우회하지 않습니다.
        var cellCounts = new System.Collections.Generic.Dictionary<string, int>();
        var pointCells = new System.Collections.Generic.Dictionary<string, string>();
        int requests = 0, actualCount = 0, maximumInitialEnemies = 0;
        for (int i = 0; i < 48; i++)
        {
            bool succeeded = launcher.TryRequestEncounter(snapshot, "Normal", 10, EncounterRequestSource.DebugWindow, out string result);
            Assert.That(succeeded, Is.True, result);
            var decision = launcher.RecentDiagnostics.Last(e => e.SpawnDecision != null).SpawnDecision;
            if (i == 0)
            {
                foreach (var c in decision.Candidates.Where(c => c.HasDistributionMetrics))
                {
                    string cell = c.CellX + ":" + c.CellY;
                    pointCells.Add(c.Id, cell);
                    cellCounts[cell] = 0;
                    maximumInitialEnemies = System.Math.Max(maximumInitialEnemies, c.EnemyCount);
                }
                Assert.That(cellCounts.Count, Is.GreaterThan(2), "다수의 Scene 스폰 셀로 회귀 현상을 검사해야 합니다.");
            }
            Assert.That(decision.Actuals.Count, Is.EqualTo(10));
            foreach (var actual in decision.Actuals) cellCounts[pointCells[actual.PointId]]++;
            actualCount += decision.Actuals.Count; requests++;
            yield return null;
        }
        int cellCount = cellCounts.Count, usedCells = cellCounts.Count(c => c.Value > 0);
        int min = cellCounts.Values.Min(), max = cellCounts.Values.Max();
        Debug.Log($"Spatial spawn integration: requests={requests}, actual={actualCount}, available cells={cellCount}, used cells={usedCells}, min={min}, max={max}, initial enemy max={maximumInitialEnemies}.");

        var liveCells = new System.Collections.Generic.HashSet<string>();
        int liveActual = 0;
        for (int i = 0; i < 24; i++)
        {
            // 실시간 요청도 동일 Planner 이력을 사용하고, 이동 중인 적의 최신 Grid를 조회합니다.
            yield return new WaitForSecondsRealtime(0.1f);
            bool succeeded = launcher.TryRequestCurrentEncounter("Normal", 10, EncounterRequestSource.DebugWindow, out string result);
            Assert.That(succeeded, Is.True, result);
            var decision = launcher.RecentDiagnostics.Last(e => e.SpawnDecision != null).SpawnDecision;
            foreach (var actual in decision.Actuals) liveCells.Add(pointCells[actual.PointId]);
            liveActual += decision.Actuals.Count;
        }
        Debug.Log($"Spatial spawn live integration: requests=24, actual={liveActual}, used cells={liveCells.Count}/{cellCount}.");
        yield return new ExitPlayMode();
        Assert.That(usedCells, Is.EqualTo(cellCount), "반복 요청은 전선 거리에 관계없이 모든 적합 셀에 생성 기회를 제공해야 합니다.");
        Assert.That(max - min, Is.LessThanOrEqualTo(maximumInitialEnemies + 10), "복제 지점의 수가 셀별 생성 비중을 배수로 늘리면 안 됩니다.");
        Assert.That(liveActual, Is.EqualTo(240));
        Assert.That(liveCells.Count, Is.GreaterThanOrEqualTo((int)System.Math.Ceiling(cellCount * 0.75)), "실시간 Grid 입력에서도 일부 가까운 스포너만 반복 선택하면 안 됩니다.");
    }

    [UnityTest]
    [Explicit("Stage 1 통합 Scene과 NGO Host가 필요한 경우에만 실행")]
    public IEnumerator InitialStage_StartsEncounterWithoutDebugRequest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();

        EncounterSceneBinding launcher = Object.FindAnyObjectByType<EncounterSceneBinding>();
        Assert.That(launcher, Is.Not.Null, "Initial_Stage의 EnemyNetworkTestLauncher가 로드되어야 합니다.");

        float deadline = Time.realtimeSinceStartup + 20f;
        int spawnedEnemies = 0;
        bool serverStarted = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            // Scene에 별도 Executor가 있어도 이 Match에 연결한 실제 실행기의 생성만 확인합니다.
            EnemySpawnExecutor executor = launcher.ExecutionTarget;
            spawnedEnemies = executor != null ? executor.CountForMatch(launcher.BoundMatchId) : 0;
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
        var launcher = Object.FindAnyObjectByType<EncounterSceneBinding>();
        if (launcher == null) { result = "Encounter launcher missing"; return false; }
        return launcher.TryRequestCurrentEncounter("Normal", 10, EncounterRequestSource.DebugWindow, out result);
    }
}

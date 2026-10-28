// 실제 Initial_Stage Weather → Match projection → Enemy 정책 → NGO 생성 경로를 검사합니다.
// 별도 검증용 정책을 주입하며 최종 밸런스 및 두 프로세스 Client 통합과 구분합니다.
using System.Collections;
using System.Linq;
using Map;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
public sealed class MatchEnvironmentIntegrationTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var app = Object.FindAnyObjectByType<AppRoot>();
        if (app != null) app.EndCurrentMatch();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator AuthoredWeatherChangesContext_EnemyConsumesRevisionWithoutAutomaticRequest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        yield return new EnterPlayMode();
        var launcher = Object.FindAnyObjectByType<EnemyNetworkTestLauncher>();
        var app = Object.FindAnyObjectByType<AppRoot>();
        var weather = Object.FindAnyObjectByType<WeatherManager>();
        Assert.That(weather, Is.Not.Null, "Scene에 배치된 실제 Weather source가 필요합니다.");
        float deadline = Time.realtimeSinceStartup + 20;
        while (app.CurrentEnvironment == null || !launcher.RecentDiagnostics.Any(item => item.SpawnDecision != null))
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        var initial = app.CurrentEnvironment;
        Assert.That(initial.HasWeather, Is.True);
        Assert.That(initial.HasTimeOfDay, Is.False, "존재하지 않는 시간대를 낮으로 추정하면 안 됩니다.");
        Assert.That(initial.Forecast, Is.Not.Empty);
        int requests = launcher.RecentDiagnostics.Count(item => item.Kind == GameDiagnosticKind.RequestCompleted);
        for (int i = 0; i < 128 && weather.WeatherId.Value != "rain"; i++) weather.ChangeWeather();
        Assert.That(weather.WeatherId.Value, Is.EqualTo("rain"));
        var changed = app.CurrentEnvironment;
        Assert.That(changed.WeatherId, Is.EqualTo(weather.WeatherId));
        Assert.That(changed.Revision, Is.GreaterThanOrEqualTo(initial.Revision));
        Assert.That(launcher.RecentDiagnostics.Count(item => item.Kind == GameDiagnosticKind.RequestCompleted), Is.EqualTo(requests),
            "환경 변경만으로 적을 추가 생성하면 안 됩니다.");

        var profile = ScriptableObject.CreateInstance<EncounterEnvironmentProfile>();
        var authoring = new SerializedObject(profile);
        var rules = authoring.FindProperty("rules"); rules.arraySize = 1;
        var rule = rules.GetArrayElementAtIndex(0);
        rule.FindPropertyRelative("ruleId").stringValue = "validation-rain-half";
        rule.FindPropertyRelative("weatherId").stringValue = "rain";
        rule.FindPropertyRelative("timeOfDayId").stringValue = "";
        rule.FindPropertyRelative("presetOverride").stringValue = "";
        rule.FindPropertyRelative("budgetScale").floatValue = .5f;
        authoring.ApplyModifiedPropertiesWithoutUndo();
        var consumer = new SerializedObject(launcher);
        consumer.FindProperty("environmentProfile").objectReferenceValue = profile;
        consumer.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(launcher.TryRequestCurrentEncounter("Normal", 10, EncounterRequestSource.DebugWindow, out string reason), Is.True, reason);
        var decision = launcher.RecentDiagnostics.Last(item => item.SpawnDecision != null).SpawnDecision;
        Assert.That(decision.Environment, Is.SameAs(changed));
        Assert.That(decision.EnvironmentPolicy, Is.EqualTo("validation-rain-half"));
        Assert.That(decision.Actuals.Count, Is.EqualTo(5), "요청 한도 10이 실제 Enemy 정책에서 5로 조정되어야 합니다.");
        var evidence = TeamHJD.Game.Editor.GameDebugSpawnDecision.Copy(launcher.RecentDiagnostics.Last(item => item.SpawnDecision != null));
        var run = new TeamHJD.Game.Editor.GameDebugRun { MatchId = changed.MatchId.ToString() };
        run.SpawnDecisions.Add(evidence);
        var imported = TeamHJD.Game.Editor.GameDebugExport.FromJson(TeamHJD.Game.Editor.GameDebugExport.ToJson(run));
        Assert.That(imported.SpawnDecisions[0].EnvironmentRevision, Is.EqualTo(changed.Revision));
        Assert.That(imported.SpawnDecisions[0].EnvironmentPolicy, Is.EqualTo("validation-rain-half"));

        // 잘못된 실제 Feature 정책의 예외도 확정 결과로 기록하고 자동 재실행하지 않습니다.
        int aliveBeforeFailure = launcher.ActiveEnemyCount;
        rule.FindPropertyRelative("budgetScale").floatValue = float.NaN;
        authoring.ApplyModifiedPropertiesWithoutUndo();
        // Scene의 검증된 발행자는 기존 호환 진입점이 선택합니다.
        Assert.That(launcher.TryRequestCurrentEncounter("Normal", 10, EncounterRequestSource.DebugWindow, out _), Is.False);
        var failure = launcher.RecentDiagnostics.Last(item => item.Kind == GameDiagnosticKind.RequestFailed);
        Assert.That(failure.IsExecutionCountUnknown, Is.True);
        Assert.That(failure.Message, Does.Contain("ExecutionFailed"));
        Assert.That(launcher.ActiveEnemyCount, Is.EqualTo(aliveBeforeFailure));
        Assert.That(failure.WithoutSpawnDecision().IsExecutionCountUnknown, Is.True);
        run.Events.Add(TeamHJD.Game.Editor.GameDebugEventEntry.Copy(failure));
        var failureRoundTrip = TeamHJD.Game.Editor.GameDebugExport.FromJson(TeamHJD.Game.Editor.GameDebugExport.ToJson(run));
        Assert.That(failureRoundTrip.Events[0].IsExecutionCountUnknown, Is.True);
        Assert.That(TeamHJD.Game.Editor.GameDebugExport.EventsCsv(run), Does.Contain("execution_counts_known"));
        rule.FindPropertyRelative("budgetScale").floatValue = .5f;
        authoring.ApplyModifiedPropertiesWithoutUndo();

        // Client World 표현은 수신값만 적용하고 자체 Random/CountTick을 진행하지 않습니다.
        var view = new GameObject("validation-replica-weather").AddComponent<WeatherManager>();
        using (view.BindReplica(changed.MatchId))
        {
            view.ApplyReplica(changed);
            view.ChangeWeather(); view.TickWeather(.1f, 100);
            Assert.That(view.WeatherId, Is.EqualTo(changed.WeatherId));
            CollectionAssert.AreEqual(changed.Forecast, view.Forecast);
        }
        Object.Destroy(view.gameObject);
        long finalRevision = changed.Revision;
        var oldSource = app.NetworkEnvironments.Source;
        var binding = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        Assert.That(binding.TryEndMatch(), Is.True);
        weather.ChangeWeather();
        Assert.That(oldSource.Snapshot, Is.Null);
        Assert.That(app.CurrentEnvironment, Is.Null);
        Object.Destroy(profile);
        Debug.Log($"S2 actual Weather/Match/Enemy: environment r{finalRevision}, rain policy planned/spawned 5; no weather-triggered request; time-of-day unavailable.");
        yield return new ExitPlayMode();
    }
}

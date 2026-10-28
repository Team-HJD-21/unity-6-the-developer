// 제작 SpawnPoint 없는 실제 위치에서 공통 요청·Enemy 생성·공간/사망 추적·Match 정리를 검사합니다.
// 검증용 source를 등록하며 나무 AI나 공중 연출을 구현한 것으로 표시하지 않습니다.
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
public sealed class EncounterOriginIntegrationTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var app = Object.FindAnyObjectByType<AppRoot>(); if (app != null) app.EndCurrentMatch();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator WorldAndSourceOriginsUseRealEnemyTrackingAndReleaseOnMatchRestart()
    {
        const string stage = "Assets/Scenes/Gameplay/Initial_Stage.unity";
        EditorSceneManager.OpenScene(stage);
        yield return new EnterPlayMode();
        var app = Object.FindAnyObjectByType<AppRoot>();
        var launcher = Object.FindAnyObjectByType<EnemyNetworkTestLauncher>();
        float deadline = Time.realtimeSinceStartup + 20;
        while (launcher.ActiveEnemyCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(launcher.ActiveEnemyCount, Is.GreaterThan(0));
        var snapshot = app.CurrentBattlefieldSnapshot;
        Vector3 location = default; bool found = false;
        var points = launcher.ExecutionTarget.SpawnPointList.GetRegisteredSpawnPoints();
        foreach (var cell in snapshot.Grid.Cells.Where(cell => cell.PlayerCount == 0 && cell.TurretCount == 0))
        {
            var grid = snapshot.Grid.Configuration;
            var center = new Vector3((float)(grid.MinimumX + (cell.X + .5) * grid.CellWidth),
                (float)(grid.MinimumY + (cell.Y + .5) * grid.CellHeight), 0);
            if (!NavMesh.SamplePosition(center, out var hit, .1f, NavMesh.AllAreas) ||
                points.Any(point => Vector2.Distance(point.Position, hit.position) < 8)) continue;
            location = hit.position; found = true; break;
        }
        Assert.That(found, Is.True, "A valid NavMesh location away from all authored SpawnPoints is required.");
        var area = new SpawnArea(new BattlefieldPoint(location.x, location.y), 2, 2);
        var match = app.CurrentMatchId;
        var issuer = app.CurrentMatchConfig.Players[0].PlayerId;
        var worldRequest = Request(match, issuer, EncounterSpawnOrigin.AtWorld(area));
        var world = launcher.Submit(worldRequest, issuer);
        Assert.That(world.Status, Is.EqualTo(EncounterRequestStatus.Spawned), world.Reason);
        Assert.That(world.SpawnedCount, Is.EqualTo(10));
        var worldEvent = launcher.RecentDiagnostics.Last(item => item.SpawnDecision != null);
        Assert.That(worldEvent.ExecutionContext.RequestGuid, Is.EqualTo(worldRequest.Context.RequestId));
        Assert.That(worldEvent.SpawnDecision.Placement.Origin.Kind, Is.EqualTo(SpawnOriginKind.WorldArea));
        Assert.That(worldEvent.SpawnDecision.Actuals.All(item => string.IsNullOrEmpty(item.PointId)), Is.True,
            "World origins must not be disguised as authored SpawnPoint IDs.");
        int beforeReplay = launcher.ActiveEnemyCount;
        Assert.That(launcher.Submit(worldRequest, issuer).IsReplay, Is.True);
        Assert.That(launcher.ActiveEnemyCount, Is.EqualTo(beforeReplay));
        Assert.That(((IEncounterOriginRequestTarget)launcher).RequestOrigin("Normal", 5, EncounterRequestSource.DebugWindow,
            EncounterSpawnOrigin.AtWorld(new SpawnArea(area.Center)), Guid.NewGuid()).SpawnedCount, Is.EqualTo(5));
        AssertAuthoredOrigin(launcher, app.CurrentBattlefieldSnapshot);

        var registry = app.CurrentSpawnSources;
        var source = registry.Register(match, new DefinitionId("validation-tree"), area);
        var sourceRequest = Request(match, issuer, EncounterSpawnOrigin.FromSource(source.Reference));
        var spawned = launcher.Submit(sourceRequest, issuer);
        Assert.That(spawned.Status, Is.EqualTo(EncounterRequestStatus.Spawned), spawned.Reason);
        var sourceEvent = launcher.RecentDiagnostics.Last(item => item.SpawnDecision != null);
        Assert.That(sourceEvent.ExecutionContext.Origin.Source.Generation, Is.EqualTo(source.Reference.Generation));
        int beforeRemoval = launcher.ActiveEnemyCount;
        var cancelledRequest = Request(match, issuer, sourceRequest.Origin);
        Assert.That(launcher.TryScheduleEncounter(cancelledRequest, 5, out var cancelled), Is.True);
        Assert.That(cancelled.IsPending, Is.True);
        source.Dispose();
        Assert.That(source.Cancellation.IsCancellationRequested, Is.True);
        Assert.That(launcher.ActiveEnemyCount, Is.EqualTo(beforeRemoval), "Removing a source must preserve its already spawned enemies.");
        Assert.That(cancelled.IsCancelled, Is.True);
        using (var partialSource = registry.Register(match, new DefinitionId("validation-partial"), area))
            AssertPartialSource(launcher, partialSource, Request(match, issuer, EncounterSpawnOrigin.FromSource(partialSource.Reference)), issuer);
        Assert.That(launcher.Submit(Request(match, issuer, sourceRequest.Origin), issuer).Status, Is.EqualTo(EncounterRequestStatus.OriginUnavailable));
        Assert.That(launcher.Submit(Request(match, issuer, EncounterSpawnOrigin.AtWorld(area, SpawnDeliveryMode.AirDrop)), issuer).Status,
            Is.EqualTo(EncounterRequestStatus.UnsupportedDelivery), "Unimplemented delivery must not silently spawn on the ground.");
        var delayedRequest = Request(match, issuer, EncounterSpawnOrigin.AtWorld(area));
        Assert.That(launcher.TryScheduleEncounter(delayedRequest, .2, out var delayed), Is.True);
        Assert.That(launcher.TryScheduleEncounter(delayedRequest, .2, out var duplicate), Is.True);
        Assert.That(duplicate, Is.SameAs(delayed));
        Assert.That(launcher.TryScheduleEncounter(delayedRequest, .3, out _), Is.False);
        float previousScale = Time.timeScale;
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(delayed.IsPending, Is.True, "Feature wait must use Match game time, not realtime.");
        Time.timeScale = previousScale;
        deadline = Time.realtimeSinceStartup + 5;
        while (delayed.IsPending && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(delayed.Result?.HasSpawned, Is.True, delayed.Reason);
        Assert.That(HasEvent(launcher, GameDiagnosticKind.EnemySpawned, cancelledRequest.Context.RequestId), Is.False);
        var actual = sourceEvent.SpawnDecision.Actuals[0];
        Assert.That(ContainsPosition(launcher, actual.EntityId), Is.True);
        deadline = Time.realtimeSinceStartup + 3;
        while (!ContainsGridEnemy(app, actual.EntityId) &&
            Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(ContainsGridEnemy(app, actual.EntityId), Is.True);
        var health = FindHealth(actual.EntityId);
        health.TakeDamage(health.MaxHealth);
        Assert.That(HasEvent(launcher, GameDiagnosticKind.EnemyDied, sourceRequest.Context.RequestId), Is.True);
        AssertCapture(sourceEvent);
        RecordCosts(Object.FindAnyObjectByType<InitialStageCompositionRoot>(), launcher);
        Assert.That(launcher.TryScheduleEncounter(Request(match, issuer, EncounterSpawnOrigin.AtWorld(area)), 30, out var endCancelled), Is.True);
        Object.FindAnyObjectByType<InitialStageCompositionRoot>().RegionBinding.ControlUnit.GetComponent<ControlUnitHealth>().ApplyDamage(int.MaxValue);
        Assert.That(endCancelled.IsCancelled, Is.True);
        Assert.That(launcher.PendingSpawnRequests, Is.Zero);
        Assert.That(launcher.ActiveEnemyCount, Is.Zero);
        Assert.That(launcher.ExecutionTarget.SpawnedEnemyCount, Is.Zero);
        Assert.That(app.NetworkMatches.TryEnd(), Is.True);
        var reload = app.SceneTransitions.ReplaceAsync(stage, CancellationToken.None);
        while (!reload.IsCompleted) yield return null;
        reload.GetAwaiter().GetResult();
        deadline = Time.realtimeSinceStartup + 15;
        while (app.CurrentMatchId.IsEmpty && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(app.CurrentMatchId, Is.Not.EqualTo(match));
        Assert.That(app.CurrentSpawnSources, Is.Not.SameAs(registry));
        Assert.That(app.CurrentSpawnSources.Resolve(sourceRequest.Origin), Is.Null);
        Debug.Log("S3 actual: world/source origin → NGO Enemy → common spatial/death/removal tracking → CU completion cleanup → fresh Match.");
        yield return new ExitPlayMode();
    }
    private static EncounterRequest Request(MatchId match, PlayerId issuer, EncounterSpawnOrigin origin)
        => new EncounterRequest(new MatchRequestContext(match, Guid.NewGuid(), issuer), new DefinitionId("Normal"), 10,
            EncounterRequestSource.RuntimeUI, origin, Guid.NewGuid());
    // EnterPlayMode는 domain reload를 수행하므로 coroutine 바깥 helper에서 predicate closure를 생성합니다.
    private static bool HasEvent(EnemyNetworkTestLauncher launcher, GameDiagnosticKind kind, Guid request)
        => launcher.RecentDiagnostics.Any(item => item.Kind == kind && item.ExecutionContext?.RequestGuid == request);
    private static bool ContainsPosition(EnemyNetworkTestLauncher launcher, string entity)
        => launcher.ExecutionTarget.CaptureEnemyPositions().Any(item => item.EntityId.Value == "enemy-" + entity);
    private static bool ContainsGridEnemy(AppRoot app, string entity)
        => app.CurrentBattlefieldSnapshot.Grid.Cells.Any(cell => cell.EnemyIds.Any(id => id.Value == "enemy-" + entity));
    private static EnemyHealth FindHealth(string entity)
        => Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Single(item => item.GetComponent<NetworkObject>().NetworkObjectId.ToString() == entity);
    private static void AssertCapture(GameDiagnosticEvent item)
    {
        var run = new TeamHJD.Game.Editor.GameDebugRun { MatchId = item.MatchId.ToString() };
        run.SpawnDecisions.Add(TeamHJD.Game.Editor.GameDebugSpawnDecision.Copy(item));
        var imported = TeamHJD.Game.Editor.GameDebugExport.FromJson(TeamHJD.Game.Editor.GameDebugExport.ToJson(run));
        var decision = imported.SpawnDecisions.Single();
        Assert.That(decision.OriginKind, Is.EqualTo("DynamicSource"));
        Assert.That(decision.SpawnSourceGeneration, Is.EqualTo(item.ExecutionContext.Origin.Source.Generation.ToString("N")));
        Assert.That(decision.RequestGuid, Is.EqualTo(item.ExecutionContext.RequestGuid.ToString("N")));
        Assert.That(decision.CauseEventId, Is.EqualTo(item.ExecutionContext.CauseEventId.ToString("N")));
        Assert.That(decision.HasPlanningTiming && decision.HasSpawningTiming, Is.True);
        Assert.That(decision.PlanningMilliseconds, Is.GreaterThanOrEqualTo(0));
    }
    private static void AssertAuthoredOrigin(EnemyNetworkTestLauncher launcher, BattlefieldSpatialSnapshot snapshot)
    {
        var point = launcher.ExecutionTarget.SpawnPointList.GetAvailableSpawnPoints().First(item =>
            snapshot.Grid.TryGetCellAt(new BattlefieldPoint(item.Position.x, item.Position.y), out var cell) && cell.PlayerCount == 0 && cell.TurretCount == 0);
        var result = ((IEncounterOriginRequestTarget)launcher).RequestOrigin("Normal", 5, EncounterRequestSource.DebugWindow,
            EncounterSpawnOrigin.AtPoint(new DefinitionId(point.SpawnPointId)), Guid.NewGuid());
        Assert.That(result.SpawnedCount, Is.EqualTo(5), result.Reason);
    }
    private static void AssertPartialSource(EnemyNetworkTestLauncher launcher, IEncounterSpawnSourceLease source, EncounterRequest request, PlayerId issuer)
    {
        Action<EnemyLifecycleObservation> remove = item =>
        {
            if (item.Kind == GameDiagnosticKind.EnemySpawned && item.Context.RequestGuid == request.Context.RequestId) source.Dispose();
        };
        var executor = launcher.ExecutionTarget;
        executor.LifecycleObserved += remove;
        try
        {
            var result = launcher.Submit(request, issuer);
            Assert.That(result.Status, Is.EqualTo(EncounterRequestStatus.PartialSpawn), result.Reason);
            Assert.That(result.SpawnedCount, Is.EqualTo(1));
            Assert.That(launcher.Submit(request, issuer).IsReplay, Is.True);
        }
        finally { executor.LifecycleObserved -= remove; }
    }
    private static void RecordCosts(InitialStageCompositionRoot scene, EnemyNetworkTestLauncher launcher)
    {
        var decisions = launcher.RecentDiagnostics.Where(item => item.SpawnDecision != null).Select(item => item.SpawnDecision).ToArray();
        Debug.Log($"CORE_S3_COST samples={decisions.Length}; spatial-latest={scene.LastSpatialRefreshMilliseconds:F3}ms; " +
            $"plan-mean={decisions.Average(item => item.PlanningMilliseconds):F3}ms; spawn-commit-mean={decisions.Average(item => item.SpawningMilliseconds):F3}ms; " +
            $"alive={launcher.ActiveEnemyCount}; scaled game-time={launcher.GameSeconds:F2}s. Editor batch sample, not a frame-budget guarantee.");
    }
}

// 실제 Initial_Stage의 지역 정책·터렛 활성·점령 회수를 검증합니다.
// 1인 로컬 Match 검증이며 두 Client 복제나 Editor 화면 인수를 대신하지 않습니다.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class RegionSceneIntegrationTests
{
    [Test]
    public void ClientBindingUsesReceivedGridWithoutCreatingLocalAuthority()
    {
        var owner = new GameObject("Client region binding");
        var controller = owner.AddComponent<FieldTurretController>();
        var binding = owner.AddComponent<RegionSceneBinding>();
        var serialized = new SerializedObject(binding);
        serialized.FindProperty("_controller").objectReferenceValue = controller;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var match = new MatchId("replica-match"); var descriptor = System.Guid.NewGuid();
        using var replica = new RegionGridReplica(match, descriptor);
        try
        {
            var snapshot = RegionGridSnapshot.FromCapturedCells(match, System.Guid.NewGuid(), 3, RegionPolicyMode.Dynamic,
                new RegionGridDefinition(BattlefieldGridConfiguration.Default), new[] { 1, 2 });
            replica.Apply(new RegionReplicationFrame(descriptor, 1, snapshot), true);
            binding.BindReplica(replica);
            Assert.That(binding.Source.Snapshot, Is.SameAs(snapshot));
            Assert.That(controller.Regions, Is.SameAs(replica));
            Assert.That(binding.HasWriteAuthority, Is.False);
            Assert.That(binding.ResetPolicy(RegionPolicyMode.Static, true), Is.False);
            Assert.That(binding.TryUnlockThroughZone(2), Is.False);
            Assert.That(binding.LastEvaluation, Is.Null);
            binding.UnbindMatch();
            Assert.That(binding.Source, Is.Null);
            Assert.That(controller.Regions, Is.Null);
        }
        finally { Object.DestroyImmediate(owner); }
    }
    private SceneSetup[] _setup;
    [SetUp]
    public void RememberScenes()
    {
        // EnterPlayMode의 도메인 재로드 뒤에도 SetUp이 호출되므로 Editor 전용 조회를 반복하지 않습니다.
        if (!Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup();
    }
    [UnityTearDown]
    public IEnumerator RestoreScenes()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (_setup != null && _setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_setup);
    }

    [UnityTest]
    public IEnumerator InitialStage_StaticToDynamicCaptureAndReleaseKeepsRemainingTurretsActive()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
        var binding = Object.FindAnyObjectByType<RegionSceneBinding>();
        Assert.That(binding, Is.Not.Null);
        Assert.That(binding.Source, Is.Not.Null);
        Assert.That(binding.Source.Snapshot.Mode, Is.EqualTo(RegionPolicyMode.Static));
        Assert.That(binding.Source.Snapshot.CapturedCellCount, Is.GreaterThan(0));
        Assert.That(binding.ResetPolicy(RegionPolicyMode.Dynamic, false), Is.False);
        Assert.That(binding.Source.Snapshot.Mode, Is.EqualTo(RegionPolicyMode.Static));
        Assert.That(binding.ResetPolicy(RegionPolicyMode.Dynamic, true), Is.True);

        var controller = Object.FindAnyObjectByType<FieldTurretController>();
        Assert.That(controller.UnlockZones, Is.Not.Null, "Scene composition must explicitly configure D's CU zones before binding Core access.");
        Assert.That(controller.UnlockedZoneCount, Is.EqualTo(1));
        var turrets = Object.FindObjectsByType<FieldTurretBase>(FindObjectsSortMode.None)
            .Where(turret => turret.isActiveAndEnabled && !turret.IsDestroyed)
            .OrderBy(turret => turret.EffectivePower).ToArray();
        Assert.That(turrets.All(turret => !turret.IsLocked), Is.True);
        Assert.That(turrets.All(turret => !turret.IsActivated), Is.True);
        FieldTurretBase[] selected = null;
        for (int a = 0; a < turrets.Length && selected == null; a++)
            for (int b = a + 1; b < turrets.Length && selected == null; b++)
                for (int c = b + 1; c < turrets.Length && selected == null; c++)
                {
                    Vector2 ab = turrets[b].transform.position - turrets[a].transform.position;
                    Vector2 ac = turrets[c].transform.position - turrets[a].transform.position;
                    if (Mathf.Abs(ab.x * ac.y - ab.y * ac.x) > .001f &&
                        turrets[a].EffectivePower + turrets[b].EffectivePower + turrets[c].EffectivePower <= controller.CurrentPower)
                        selected = new[] { turrets[a], turrets[b], turrets[c] };
                }
        Assert.That(selected, Is.Not.Null, "Actual authored power must permit three non-collinear turrets.");
        long beforeRevision = binding.Source.Snapshot.Revision;
        var beforeEvaluation = binding.LastEvaluation;
        foreach (FieldTurretBase turret in selected)
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));

        float deadline = Time.realtimeSinceStartup + 3;
        // CU seed는 처음부터 존재합니다. 셀 집합이 같아 revision이 유지되는 경우에는 새 기하 평가도 확인합니다.
        while (binding.Source.Snapshot.Revision == beforeRevision && object.ReferenceEquals(binding.LastEvaluation, beforeEvaluation) &&
               Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(binding.LastEvaluation, Is.Not.SameAs(beforeEvaluation));
        Assert.That(binding.Source.Snapshot.CapturedCellCount, Is.GreaterThan(0));
        Assert.That(binding.LastEvaluation.Geometry.Vertices.Any(vertex => vertex.EntityId.Value == DynamicRegionCapturePolicy.ControlUnitNodeId), Is.True);
        Assert.That(binding.LastEvaluation.Geometry.Triangles.Count, Is.GreaterThan(0));
        Vector3 cuPosition = binding.ControlUnit.position;
        Assert.That(binding.Source.Snapshot.TryIsCapturedAt(new BattlefieldPoint(cuPosition.x, cuPosition.y), out bool cuCaptured), Is.True);
        Assert.That(cuCaptured, Is.True);
        beforeRevision = binding.Source.Snapshot.Revision;
        beforeEvaluation = binding.LastEvaluation;
        Assert.That(selected[0].RequestActivation(false), Is.EqualTo(FieldTurretActivationResult.Deactivated));
        deadline = Time.realtimeSinceStartup + 3;
        while (binding.Source.Snapshot.Revision == beforeRevision && object.ReferenceEquals(binding.LastEvaluation, beforeEvaluation) &&
               Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(binding.LastEvaluation, Is.Not.SameAs(beforeEvaluation));
        Assert.That(binding.Source.Snapshot.CapturedCellCount, Is.GreaterThanOrEqualTo(1));
        Vector2 fromCuA = selected[1].transform.position - cuPosition;
        Vector2 fromCuB = selected[2].transform.position - cuPosition;
        if (Mathf.Abs(fromCuA.x * fromCuB.y - fromCuA.y * fromCuB.x) > .001f)
            Assert.That(binding.LastEvaluation.Geometry.Triangles.Count, Is.GreaterThan(0));
        Assert.That(selected[1].IsActivated, Is.True);
        Assert.That(selected[2].IsActivated, Is.True);
        Assert.That(selected[1].IsLocked || selected[2].IsLocked, Is.False);
        Assert.That(controller.GetSnapshot().ReservedPower, Is.EqualTo(selected[1].EffectivePower + selected[2].EffectivePower));
        beforeEvaluation = binding.LastEvaluation;
        Assert.That(selected[1].RequestActivation(false), Is.EqualTo(FieldTurretActivationResult.Deactivated));
        Assert.That(selected[2].RequestActivation(false), Is.EqualTo(FieldTurretActivationResult.Deactivated));
        deadline = Time.realtimeSinceStartup + 3;
        while (object.ReferenceEquals(binding.LastEvaluation, beforeEvaluation) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(binding.Source.Snapshot.CapturedCellCount, Is.EqualTo(1));
        Assert.That(binding.LastEvaluation.Geometry.Triangles.Count, Is.Zero);
        Assert.That(binding.ResetPolicy(RegionPolicyMode.Static, true), Is.True);
        Assert.That(binding.Source.Snapshot.Mode, Is.EqualTo(RegionPolicyMode.Static));
        yield return new ExitPlayMode();
    }
}

// 실제 Initial_Stage의 지역 정책·터렛 활성·점령 회수를 검증합니다.
// 1인 로컬 Match 검증이며 두 Client 복제나 Editor 화면 인수를 대신하지 않습니다.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class RegionSceneIntegrationTests
{
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
        foreach (FieldTurretBase turret in selected)
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));

        float deadline = Time.realtimeSinceStartup + 3;
        while (binding.Source.Snapshot.CapturedCellCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(binding.Source.Snapshot.CapturedCellCount, Is.GreaterThan(0));
        Assert.That(binding.LastEvaluation.Geometry.Triangles.Count, Is.EqualTo(1));
        Assert.That(selected[0].RequestActivation(false), Is.EqualTo(FieldTurretActivationResult.Deactivated));
        deadline = Time.realtimeSinceStartup + 3;
        while (binding.Source.Snapshot.CapturedCellCount != 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(binding.Source.Snapshot.CapturedCellCount, Is.Zero);
        Assert.That(selected[1].IsActivated, Is.True);
        Assert.That(selected[2].IsActivated, Is.True);
        Assert.That(selected[1].IsLocked || selected[2].IsLocked, Is.False);
        Assert.That(controller.GetSnapshot().ReservedPower, Is.EqualTo(selected[1].EffectivePower + selected[2].EffectivePower));
        Assert.That(binding.ResetPolicy(RegionPolicyMode.Static, true), Is.True);
        Assert.That(binding.Source.Snapshot.Mode, Is.EqualTo(RegionPolicyMode.Static));
        yield return new ExitPlayMode();
    }
}

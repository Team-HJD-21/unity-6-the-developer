// 실제 Initial_Stage의 CU 정적 초기화와 동적 모드의 정적 잠금 미적용을 확인합니다.
// 잘못된 Scene 설정은 Match/Player 생성 전에 거부하는지 별도로 검증합니다.
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class InitialStageUnlockInitializationTests
{
    private const string Stage = "Assets/Scenes/Gameplay/Initial_Stage.unity";
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
        {
            Object.FindAnyObjectByType<AppRoot>()?.EndCurrentMatch();
            yield return new ExitPlayMode();
        }
    }
    [UnityTest]
    public IEnumerator StaticEntryInitializesActualControllerFromCuAndInspectorSettings()
    {
        EditorSceneManager.OpenScene(Stage);
        yield return new EnterPlayMode(); yield return null;
        RegionSceneBinding region = Object.FindAnyObjectByType<RegionSceneBinding>();
        Assert.That(region.Source, Is.Not.Null);
        Assert.That(region.Source.Snapshot.Mode, Is.EqualTo(RegionPolicyMode.Static));
        FieldTurretController controller = region.Controller;
        Assert.That(controller.UnlockZones, Is.Not.Null, "실제 D 초기화가 생략되면 안 됩니다.");
        var expected = controller.CreateUnlockZoneDefinition(region.ControlUnit.position);
        Assert.That(controller.UnlockZones.ZoneCount, Is.EqualTo(expected.ZoneCount));
        Assert.That(controller.UnlockZones.CuCellX, Is.EqualTo(expected.CuCellX));
        Assert.That(controller.UnlockZones.CuCellY, Is.EqualTo(expected.CuCellY));
        Assert.That(controller.UnlockedZoneCount, Is.EqualTo(1));
        Assert.That(region.Source.Snapshot.CapturedCellCount, Is.GreaterThan(0));
        Assert.That(Object.FindAnyObjectByType<AppRoot>().CurrentMatchId.IsEmpty, Is.False);
        Debug.Log("CU_UNLOCK_VERIFIED: actual controller initialized before Core region policy; first zone captured.");
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator DynamicEntryDoesNotConfigureStaticDistanceLocks()
    {
        EditorSceneManager.OpenScene(Stage);
        var authoring = new SerializedObject(Object.FindAnyObjectByType<RegionSceneBinding>());
        authoring.FindProperty("_initialMode").enumValueIndex = (int)RegionPolicyMode.Dynamic;
        authoring.ApplyModifiedPropertiesWithoutUndo();
        yield return new EnterPlayMode(); yield return null;
        RegionSceneBinding region = Object.FindAnyObjectByType<RegionSceneBinding>();
        Assert.That(region.Source.Snapshot.Mode, Is.EqualTo(RegionPolicyMode.Dynamic));
        Assert.That(region.Controller.UnlockZones, Is.Null, "동적 진입에 정적 거리 잠금을 적용하면 안 됩니다.");
        AssertDynamicTurretsUnlocked(region);
        yield return new ExitPlayMode();
    }
    // EnterPlayMode의 domain reload를 넘는 iterator 지역 변수를 람다로 캡처하지 않습니다.
    private static void AssertDynamicTurretsUnlocked(RegionSceneBinding region)
        => Assert.That(FieldTurretInstanceRegistry.GetAll()
            .Where(turret => turret.gameObject.scene == region.gameObject.scene).All(turret => !turret.IsLocked), Is.True);
    [TestCase("missing-cu")]
    [TestCase("outside-grid")]
    [TestCase("inactive-controller")]
    [TestCase("duplicate-binding")]
    public void InvalidRegionAuthoringIsRejectedBeforeStartingMatch(string condition)
    {
        EditorSceneManager.OpenScene(Stage);
        RegionSceneBinding region = Object.FindAnyObjectByType<RegionSceneBinding>();
        if (condition == "missing-cu")
        {
            var authoring = new SerializedObject(region);
            authoring.FindProperty("_controlUnit").objectReferenceValue = null;
            authoring.ApplyModifiedPropertiesWithoutUndo();
        }
        else if (condition == "outside-grid") region.ControlUnit.position = new Vector3(1000000, 1000000, 0);
        else if (condition == "inactive-controller") region.Controller.enabled = false;
        else new GameObject("Duplicate region").AddComponent<RegionSceneBinding>();
        var host = new RejectingHost();
        var scene = Object.FindAnyObjectByType<InitialStageCompositionRoot>();
        Assert.That(() => scene.Compose(host), Throws.Exception);
        Assert.That(host.StartCalls, Is.Zero, "제작 오류가 있으면 Match/actor를 만들기 전에 거부해야 합니다.");
        Assert.That(region.Source, Is.Null);
    }
    private sealed class RejectingHost : IAppMatchHost
    {
        public int StartCalls { get; private set; }
        public MatchId CurrentMatchId => default;
        public MatchSession StartMatch(MatchConfig config, MatchState state, IModeRules rules,
            BattlefieldSpatialInput input, BattlefieldDynamicSpatialInput participants, BattlefieldGridConfiguration grid)
        { StartCalls++; throw new InvalidOperationException("Unexpected Match creation."); }
        public void UpdateBattlefieldParticipants(BattlefieldDynamicSpatialInput input) => throw new NotSupportedException();
        public void UpdateBattlefieldTurretLayout(BattlefieldSpatialInput input) => throw new NotSupportedException();
        public void ReconfigureBattlefieldGrid(BattlefieldGridConfiguration grid) => throw new NotSupportedException();
        public MatchCompletion CompleteCurrentMatch(MatchOutcome outcome, long sequence) => throw new NotSupportedException();
        public void EndCurrentMatch() => throw new NotSupportedException();
        public bool TryEndMatch(MatchId id) => false;
    }
}

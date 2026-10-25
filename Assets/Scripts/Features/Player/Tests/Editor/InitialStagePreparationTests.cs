// 실제 제작 Scene의 CU·Controller·Grid 준비 검사가 상태를 바꾸지 않는지 확인합니다.
// 잘못된 연결은 Match/actor를 생성하기 전에 거부해야 합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Turrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class InitialStagePreparationTests
{
    private SceneSetup[] _setup;
    private InitialStageCompositionRoot _scene;
    private RegionSceneBinding _regions;
    private NetworkMatchDescriptor _descriptor;

    [SetUp]
    public void OpenAuthoredScene()
    {
        _setup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        _scene = Object.FindAnyObjectByType<InitialStageCompositionRoot>();
        _regions = _scene.RegionBinding;
        var player = new PlayerId("preflight-host");
        var config = new MatchConfig(new MatchId("preflight"), MatchMode.Story,
            new StageId("initial-stage"), new DefinitionId("difficulty"), "poc", 1,
            new[] { new PlayerLoadoutSnapshot(player, null) });
        _descriptor = new NetworkMatchDescriptor(Guid.NewGuid(), 1, config, _regions.InitialMode,
            _regions.CaptureInitialDefinition(), new[] { new MatchParticipantBinding(0, player, true) });
    }

    [TearDown]
    public void RestoreScenes() => EditorSceneManager.RestoreSceneManagerSetup(_setup);

    [Test]
    public void PreparationValidatesCuWithoutConfiguringFeatureOrCreatingMatch()
    {
        var before = _regions.Controller.UnlockZones;
        _scene.ValidateNetworkPreparation(_descriptor);
        Assert.That(_regions.Controller.UnlockZones, Is.SameAs(before));
        Assert.That(_regions.Source, Is.Null);
        Assert.That(_regions.Controller.BoundMatchId.IsEmpty, Is.True);
    }

    [Test]
    public void MissingCuIsRejectedBeforeFeatureInitialization()
    {
        var authoring = new SerializedObject(_regions);
        authoring.FindProperty("_controlUnit").objectReferenceValue = null;
        authoring.ApplyModifiedPropertiesWithoutUndo();
        Assert.Throws<InvalidOperationException>(() => _scene.ValidateNetworkPreparation(_descriptor));
        Assert.That(_regions.Source, Is.Null);
        Assert.That(_regions.Controller.BoundMatchId.IsEmpty, Is.True);
    }

    [Test]
    public void CuOutsideGridIsRejectedBeforeMatchOwnership()
    {
        _regions.ControlUnit.position = new Vector3(1000000, 1000000, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => _scene.ValidateNetworkPreparation(_descriptor));
        Assert.That(_regions.Controller.BoundMatchId.IsEmpty, Is.True);
    }

    [Test]
    public void DifferentControllerCannotBecomeASecondRegionWriter()
    {
        var other = new GameObject("Invalid second controller").AddComponent<TurretController>();
        var authoring = new SerializedObject(_regions);
        authoring.FindProperty("_controller").objectReferenceValue = other;
        authoring.ApplyModifiedPropertiesWithoutUndo();
        Assert.Throws<InvalidOperationException>(() => _scene.ValidateNetworkPreparation(_descriptor));
        Assert.That(other.BoundMatchId.IsEmpty, Is.True);
    }
}

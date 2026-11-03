// 실제 HUD 버튼이 Host 명령을 정확히 한 번 실행하고 확정 투영을 표시하는지 검사합니다.
// 마우스/해상도 인수와 원격 B actor 이동 복제 검증을 대신하지 않습니다.
using System.Collections;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Object = UnityEngine.Object;
public sealed class StageTurretCommandHudTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var app = Object.FindAnyObjectByType<AppRoot>(); if (app != null) app.EndCurrentMatch();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator ActualButtonsUseTypedCommandsHostStateAndOneSubscriptionAfterReenable()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        yield return new EnterPlayMode(); yield return null;
        var app = Object.FindAnyObjectByType<AppRoot>();
        var network = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        var hud = Object.FindAnyObjectByType<InitialStagePlayerHUD>();
        var player = Object.FindAnyObjectByType<PlayerInfo>();
        var controller = Object.FindAnyObjectByType<FieldTurretController>();
        var entities = Object.FindAnyObjectByType<TurretMatchBinding>();
        var regions = Object.FindAnyObjectByType<RegionSceneBinding>();
        var view = hud.GetComponentInChildren<StageTurretControlView>(true);
        Assert.That(hud.HasTurretCommandBinding, Is.True);
        hud.BindTurretCommands(hud.BoundMatchId, hud.BoundPlayerId, network.TurretCommands, network.TurretPresentation);
        Assert.That(network.TurretPresentation.Snapshot, Is.Not.Null);
        Assert.That(regions.ResetPolicy(RegionPolicyMode.Dynamic, true), Is.True);
        FieldTurretBase turret = FindInactive(controller, default);
        Assert.That(turret.TryGetEntityId(out EntityId firstId), Is.True);
        player.transform.position = turret.transform.position;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(hud.TrySelectTurret(firstId), Is.True);
        Click(view, "activationButton");
        Assert.That(turret.IsActivated, Is.True);
        Assert.That(network.TurretPresentation.Snapshot.TryGet(firstId, out var state), Is.True); Assert.That(state.IsActivated, Is.True);
        Assert.That(Text(view, "feedbackText"), Does.Contain("Applied by Host"));
        Click(view, "activationButton"); Assert.That(turret.IsActivated, Is.False);

        turret = FindInactive(controller, firstId); Assert.That(turret.TryGetEntityId(out EntityId id), Is.True);
        player.transform.position = turret.transform.position;
        Assert.That(hud.TrySelectTurret(id), Is.True);
        int level = turret.Definition.Level;
        Click(view, "levelUpButton"); turret = Resolve(entities, id);
        Assert.That(turret.Definition.Level, Is.EqualTo(level + 1));
        Assert.That(network.TurretPresentation.Snapshot.TryGet(id, out state), Is.True); Assert.That(state.Level, Is.EqualTo(level + 1));
        Click(view, "levelDownButton"); turret = Resolve(entities, id); Assert.That(turret.Definition.Level, Is.EqualTo(level));
        Click(view.Upgrades, "lowPowerPlus"); Assert.That(turret.RuntimeState.UpgradeLevel, Is.EqualTo(1));
        Click(view.Upgrades, "lowPowerMinus"); Assert.That(turret.RuntimeState.UpgradeLevel, Is.Zero);

        var controls = player.GetComponent<PlayerControlState>(); controls.SetInputBlocked(true);
        long sequence = network.TurretPresentation.Snapshot.ConfirmedCommandSequence;
        ButtonOf(view, "activationButton").onClick.Invoke(); controls.SetInputBlocked(false);
        Assert.That(network.TurretPresentation.Snapshot.ConfirmedCommandSequence, Is.EqualTo(sequence));
        Assert.That(turret.IsActivated, Is.False);
        hud.gameObject.SetActive(false); hud.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(hud.HasTurretCommandBinding, Is.True); Assert.That(hud.TrySelectTurret(id), Is.True);
        sequence = network.TurretPresentation.Snapshot.ConfirmedCommandSequence;
        Click(view, "activationButton"); Assert.That(turret.IsActivated, Is.True);
        Assert.That(network.TurretPresentation.Snapshot.ConfirmedCommandSequence, Is.EqualTo(sequence + 1));
        var controlUnit = Object.FindAnyObjectByType<ControlUnitHealth>();
        controlUnit.enabled = false;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(network.TurretPresentation.Snapshot.Controller.IsControlUnitAvailable, Is.False);
        controlUnit.enabled = true;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(network.TurretPresentation.Snapshot.Controller.IsControlUnitAvailable, Is.True);
        app.EndCurrentMatch(); Assert.That(hud.HasTurretCommandBinding, Is.False);
        yield return new ExitPlayMode();
    }
    private static FieldTurretBase FindInactive(FieldTurretController controller, EntityId exclude)
    {
        foreach (FieldTurretBase turret in FieldTurretInstanceRegistry.GetAll())
            if (controller.OwnsTurret(turret) && !turret.IsActivated && !turret.IsCoolingDown && turret.TryGetEntityId(out var id) && id != exclude && turret.Definition.Level == 1)
                return turret;
        Assert.Fail("No inactive level-one test turret."); return null;
    }
    private static FieldTurretBase Resolve(TurretMatchBinding entities, EntityId id)
    {
        Assert.That(entities.TryResolveLocalId(id, out int localId), Is.True);
        Assert.That(FieldTurretInstanceRegistry.TryGet(localId, out var turret), Is.True); return turret;
    }
    private static Button ButtonOf(Object view, string property) => (Button)new SerializedObject(view).FindProperty(property).objectReferenceValue;
    private static void Click(Object view, string property)
    { Button button = ButtonOf(view, property); Assert.That(button.interactable, Is.True, property); button.onClick.Invoke(); }
    private static string Text(Object view, string property) => ((TMP_Text)new SerializedObject(view).FindProperty(property).objectReferenceValue).text;
}

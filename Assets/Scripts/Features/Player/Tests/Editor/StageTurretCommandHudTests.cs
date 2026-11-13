// 실제 HUD 버튼이 Host 명령을 정확히 한 번 실행하고 확정 투영을 표시하는지 검사합니다.
// 마우스/해상도 인수와 원격 B actor 이동 복제 검증을 대신하지 않습니다.
using System.Collections;
using System.Linq;
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
        yield return EnterFieldThroughTheSpaceshipExit();
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
        yield return AssertActivationColor(view, Color.green);
        Click(view, "activationButton");
        Assert.That(turret.IsActivated, Is.True);
        Assert.That(network.TurretPresentation.Snapshot.TryGet(firstId, out var state), Is.True); Assert.That(state.IsActivated, Is.True);
        Assert.That(Text(view, "feedbackText"), Is.Empty, "확정 결과는 공통 Alert로 전달합니다.");
        yield return new WaitForSecondsRealtime(.05f);
        var alert = Object.FindAnyObjectByType<TeamHJD.Game.Presentation.AlertStripView>();
        Assert.That(alert, Is.Not.Null);
        Assert.That(alert.Message.text, Is.EqualTo("적용했습니다."));
        yield return AssertActivationColor(view, Color.red);
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
    [UnityTest]
    public IEnumerator CombatMenuKeepsWorldTimeAndOtherLeasesAndShowsTheFinalHostAutoReply()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        yield return new EnterPlayMode();
        yield return ExerciseCombatMenu();
    }

    private static IEnumerator ExerciseCombatMenu()
    {
        var app = Object.FindAnyObjectByType<AppRoot>();
        var hud = Object.FindAnyObjectByType<InitialStagePlayerHUD>();
        var vitals = Object.FindAnyObjectByType<StagePlayerVitalsView>();
        var debug = Object.FindAnyObjectByType<CombatHudDebugPanel>();
        var network = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        float deadline = Time.realtimeSinceStartup + 3;
        while ((!hud.HasLocalActor || network.TurretPresentation.Snapshot?.Controller?.IsControlUnitAvailable != true)
            && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(hud.HasLocalActor, Is.True);
        var controlUnit = network.TurretPresentation.Snapshot.Controller;
        Assert.That(controlUnit.IsControlUnitAvailable, Is.True);
        // 직접 Stage 진입 시 시작 문에 닿아 우주선 방문이 먼저 진행될 수 있습니다.
        yield return new WaitForSecondsRealtime(.5f);
        deadline = Time.realtimeSinceStartup + 3;
        while (app.LocalInput.IsBlocked && Time.realtimeSinceStartup < deadline) yield return null;
        var skillHud = Object.FindAnyObjectByType<PlayerSkillHud>();
        var presence = vitals.Player.GetComponent<TeamHJD.Game.Presentation.Scene.PlayerMatchPresence>();
        if (presence.Location == MatchPlayerLocation.Spaceship)
        {
            Assert.That(skillHud.IsFieldVisible, Is.False);
            Assert.That(skillHud.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(skillHud.Owner, Is.Not.Null, "Visibility must not detach the local actor.");
            foreach (var group in hud.GetComponentsInChildren<CombatVitalsGroup>(true)) Assert.That(group.Group.alpha, Is.Zero);
            Assert.That(debug.Panel.alpha + debug.Toast.alpha, Is.Zero);
            var exit = Object.FindAnyObjectByType<SpaceshipExit>();
            var body = vitals.Player.GetComponentsInChildren<Collider2D>().First(c => c.enabled && c.gameObject.activeInHierarchy);
            var travel = exit.ExitAsync(body);
            deadline = Time.realtimeSinceStartup + 3;
            while (!travel.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(travel.IsCompleted, Is.True);
            travel.GetAwaiter().GetResult();
            yield return null;
        }
        Assert.That(presence.Location, Is.EqualTo(MatchPlayerLocation.Field));
        Assert.That(skillHud.IsFieldVisible && skillHud.GetComponent<Canvas>().enabled, Is.True);
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        System.IDisposable otherLease = null;
        string feedback = null;
        System.Action<string> received = message => feedback = message;
        hud.AutoActivationFeedback += received;
        try
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState
                { position = new Vector2(Screen.width * .5f, Screen.height * .5f) });
            UnityEngine.InputSystem.InputSystem.Update();
            // 정식 Match의 CU는 로컬 호환 참조가 아니라 Host 확정 presentation에서 읽습니다.
            vitals.Player.transform.position = new Vector3((float)controlUnit.ControlUnitPosition.X + 5, (float)controlUnit.ControlUnitPosition.Y);
            var clock = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IMatchDayNightCycle>().First();
            var menu = app.Screens.ShowMenu();
            Assert.That(menu, Is.Not.Null);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(debug.Panel.alpha, Is.EqualTo(1).Within(.001f));
            double time = clock.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(clock.RemainingSeconds, Is.LessThan(time), "The personal menu must not pause shared world time.");

            long sequence = network.TurretPresentation.Snapshot.ConfirmedCommandSequence;
            otherLease = app.LocalInput.Acquire("HUD v6 unrelated test lease");
            debug.Auto.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(app.Screens.Top, Is.Null);
            Assert.That(app.LocalInput.IsBlocked, Is.True, "Closing this menu must preserve another owner's lease.");
            Assert.That(network.TurretPresentation.Snapshot.ConfirmedCommandSequence, Is.EqualTo(sequence));
            Assert.That(feedback, Does.Contain("현재는 요청할 수 없습니다"));
            otherLease.Dispose(); otherLease = null;
            yield return null;

            Assert.That(app.Screens.ShowMenu(), Is.Not.Null);
            yield return new WaitForSecondsRealtime(.35f);
            feedback = null;
            debug.Auto.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(app.Screens.Top, Is.Null);
            Assert.That(app.LocalInput.IsBlocked, Is.False);
            Assert.That(feedback == "자동 활성화가 시작되었습니다." || feedback == "자동 활성화 요청이 거절되었습니다.",
                Is.True, "The synchronous Host reply must survive the submit handler: " + feedback);
            Assert.That(debug.ToastLabel.text, Is.Empty, "공통 Alert와 별도 디버그 토스트를 중복 표시하지 않습니다.");
            var previousSelection = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            app.Screens.ShowMenu();
            var menuView = Object.FindObjectsByType<TeamHJD.Game.Presentation.AppPanelView>(FindObjectsSortMode.None).First(v => v.name == "Screen:Menu");
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(menuView.Settings.gameObject);
            app.Screens.ShowSettings();
            // TestRunner can resume before this frame's LateUpdate, which projects the screen stack.
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(debug.Panel.alpha + debug.Toast.alpha, Is.Zero, "Settings covers both debug controls and their toast.");
            Assert.That(app.Screens.Back(), Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(menuView.Settings.gameObject), "Back restores the selected menu action.");
            Assert.That(app.Screens.Back(), Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(previousSelection), "A covered menu must retain its original external selection.");
        }
        finally
        {
            hud.AutoActivationFeedback -= received;
            otherLease?.Dispose();
            if (mouse.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
        }
    }

    private static IEnumerator EnterFieldThroughTheSpaceshipExit()
    {
        var vitals = Object.FindAnyObjectByType<StagePlayerVitalsView>();
        float deadline = Time.realtimeSinceStartup + 4;
        while ((vitals.Player == null || Object.FindAnyObjectByType<SpaceshipExit>() == null) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(vitals.Player, Is.Not.Null);
        yield return new WaitForSecondsRealtime(.5f);
        var app = Object.FindAnyObjectByType<AppRoot>();
        deadline = Time.realtimeSinceStartup + 4;
        while (app.LocalInput.IsBlocked && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(app.LocalInput.IsBlocked, Is.False, "Finish the automatic entrance fade before requesting the normal exit.");
        var presence = vitals.Player.GetComponent<TeamHJD.Game.Presentation.Scene.PlayerMatchPresence>();
        if (presence.Location == MatchPlayerLocation.Spaceship)
        {
            var exit = Object.FindAnyObjectByType<SpaceshipExit>();
            var body = vitals.Player.GetComponentsInChildren<Collider2D>().First(c => c.enabled && c.gameObject.activeInHierarchy);
            var travel = exit.ExitAsync(body);
            deadline = Time.realtimeSinceStartup + 4;
            while (!travel.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(travel.IsCompleted, Is.True); travel.GetAwaiter().GetResult();
            yield return null;
        }
        Assert.That(presence.Location, Is.EqualTo(MatchPlayerLocation.Field));
        // 앞선 검사/에디터의 포인터가 하단 스킬 열기 영역에 남으면 준비 UI가 정상적으로 조작을 막습니다.
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null) UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(mouse.position, new Vector2(Screen.width * .5f, Screen.height * .8f));
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(Object.FindAnyObjectByType<PlayerSkillHud>().IsOpen, Is.False);
    }
    private static FieldTurretBase FindInactive(FieldTurretController controller, EntityId exclude)
    {
        foreach (FieldTurretBase turret in FieldTurretInstanceRegistry.GetAll())
            if (controller.OwnsTurret(turret) && !turret.IsDestroyed && !turret.IsLocked && !turret.IsActivated && !turret.IsCoolingDown && turret.TryGetEntityId(out var id) && id != exclude && turret.Definition.Level == 1)
                return turret;
        Assert.Fail("No inactive level-one test turret."); return null;
    }
    private static FieldTurretBase Resolve(TurretMatchBinding entities, EntityId id)
    {
        Assert.That(entities.TryResolveLocalId(id, out int localId), Is.True);
        Assert.That(FieldTurretInstanceRegistry.TryGet(localId, out var turret), Is.True); return turret;
    }
    private static Button ButtonOf(Object view, string property) => (Button)new SerializedObject(view).FindProperty(property).objectReferenceValue;
    private static IEnumerator AssertActivationColor(StageTurretControlView view, Color expected)
    {
        var label = (TMP_Text)new SerializedObject(view).FindProperty("activationText").objectReferenceValue;
        var feedback = ButtonOf(view, "activationButton").GetComponent<TeamHJD.Game.Presentation.UiButtonFeedback>();
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        feedback.OnPointerEnter(pointer);
        for (int i = 0; i < 8; i++) { yield return null; Assert.That(label.color, Is.EqualTo(expected), "Hover must preserve the activation state's text color."); }
        feedback.OnPointerExit(pointer);
        for (int i = 0; i < 4; i++) { yield return null; Assert.That(label.color, Is.EqualTo(expected)); }
    }
    private static void Click(Object view, string property)
    { Button button = ButtonOf(view, property); Assert.That(button.interactable, Is.True, property); button.onClick.Invoke(); }
    private static string Text(Object view, string property) => ((TMP_Text)new SerializedObject(view).FindProperty(property).objectReferenceValue).text;
}

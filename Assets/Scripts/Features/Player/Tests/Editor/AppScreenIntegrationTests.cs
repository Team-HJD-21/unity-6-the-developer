// 실제 UGUI 버튼으로 로컬 메뉴·CU 결과·로딩·재시작·Hub 복귀를 검증합니다.
// 자동 Button 호출이며 사람의 화면 외형/마우스 사용성 인수와 구분합니다.
using System.Collections;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class AppScreenIntegrationTests
{
    private SceneSetup[] _setup;
    [SetUp] public void RememberScenes() { if (!Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var app = Object.FindAnyObjectByType<AppRoot>(); if (app != null) app.EndCurrentMatch();
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (_setup != null && _setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_setup);
    }

    [UnityTest]
    public IEnumerator ActualMenuAndResultButtonsPreserveLocksAndDriveRestartThenHubReturn()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        yield return new EnterPlayMode(); yield return null;
        var app = Object.FindAnyObjectByType<AppRoot>();
        var player = Object.FindAnyObjectByType<PlayerControlState>();
        float originalScale = Time.timeScale;
        var menu = app.Screens.ShowMenu();
        Assert.That(app.Screens.ShowMenu(), Is.SameAs(menu));
        Assert.That(player.IsBlocked, Is.True); Assert.That(Time.timeScale, Is.EqualTo(originalScale));
        Button resume = FindButton(ScreenKind.Menu, "Primary");
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(resume.gameObject));
        player.SetInputBlocked(true); resume.onClick.Invoke();
        Assert.That(player.IsBlocked, Is.True, "UI close must not erase the Feature death/action lock.");
        player.SetInputBlocked(false); Assert.That(app.Screens.Top, Is.Null);
        var scene = Object.FindAnyObjectByType<InitialStageCompositionRoot>();
        var first = app.CurrentMatchId;
        var cu = scene.RegionBinding.ControlUnit.GetComponent<ControlUnitHealth>();
        cu.ApplyDamage(cu.CurrentHealth);
        Assert.That(app.Screens.Top.Kind, Is.EqualTo(ScreenKind.MatchResult));
        var preserved = app.Screens.Top.Completion;
        Assert.That(preserved, Is.SameAs(scene.MatchCoordinator.Completion));
        Assert.That(app.Screens.Back(), Is.False);
        FindButton(ScreenKind.MatchResult, "Primary").onClick.Invoke();
        Assert.That(app.Screens.Top.Kind, Is.EqualTo(ScreenKind.Loading));
        Assert.That(app.LocalInput.IsBlocked, Is.True);
        float deadline = Time.realtimeSinceStartup + 20;
        while ((app.SceneTransitions.IsTransitioning || app.CurrentMatchId.IsEmpty || app.Screens.Top != null) &&
            Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(app.CurrentMatchId.IsEmpty, Is.False); Assert.That(app.CurrentMatchId, Is.Not.EqualTo(first));
        Assert.That(app.Screens.Top, Is.Null); Assert.That(app.LocalInput.IsBlocked, Is.False);
        Assert.That(preserved.Result.MatchId, Is.EqualTo(first), "The old result remains safe to read after actor cleanup.");
        scene = Object.FindAnyObjectByType<InitialStageCompositionRoot>();
        cu = scene.RegionBinding.ControlUnit.GetComponent<ControlUnitHealth>();
        Assert.That(cu.CurrentHealth, Is.GreaterThan(0)); cu.ApplyDamage(cu.CurrentHealth);
        FindButton(ScreenKind.MatchResult, "Secondary").onClick.Invoke();
        deadline = Time.realtimeSinceStartup + 20;
        while ((app.SceneTransitions.IsTransitioning || app.Screens.Top != null) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/Scenes/Spaceship/SpaceShipGrayBox.unity"));
        Assert.That(app.CurrentMatchId.IsEmpty, Is.True); Assert.That(app.LocalInput.IsBlocked, Is.False);
        app.Screens.ShowMenu();
        Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        FindButton(ScreenKind.Menu, "Primary").onClick.Invoke();
        Assert.That(app.Screens.Top, Is.Null); Assert.That(app.LocalInput.IsBlocked, Is.False);
    }

    private static Button FindButton(ScreenKind kind, string name)
    {
        GameObject overlay = GameObject.Find("Screen:" + kind);
        Assert.That(overlay, Is.Not.Null);
        return overlay.transform.Find("Panel/" + name).GetComponent<Button>();
    }
}

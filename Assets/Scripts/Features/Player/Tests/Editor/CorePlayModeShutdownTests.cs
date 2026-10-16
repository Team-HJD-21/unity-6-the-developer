// 재생 종료 알림에서 Scene 파괴보다 먼저 현재 Match가 정리되는지 확인합니다.
using System.Collections;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
public sealed class CorePlayModeShutdownTests
{
    private const string RecordedKey = "TeamHJD.Core.ExitBeforeSceneDestruction";
    [UnityTearDown]
    public IEnumerator StopScene()
    {
        EditorApplication.playModeStateChanged -= RecordExit;
        if (Application.isPlaying) yield return new ExitPlayMode();
        SessionState.EraseBool(RecordedKey);
    }
    [UnityTest]
    public IEnumerator ExitSignalDisposesAppBeforeSceneObjectsAreDestroyed()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        var root = Object.FindAnyObjectByType<AppRoot>();
        Assert.That(root, Is.Not.Null);
        Assert.That(root.CurrentMatchId.IsEmpty, Is.False);
        SessionState.SetBool(RecordedKey, false);
        EditorApplication.playModeStateChanged += RecordExit;
        yield return new ExitPlayMode();
        Assert.That(SessionState.GetBool(RecordedKey, false), Is.True);
    }
    private static void RecordExit(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;
        EditorApplication.playModeStateChanged -= RecordExit;
        AppRoot[] roots = Object.FindObjectsByType<AppRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool cleaned = roots.Length > 0;
        foreach (AppRoot root in roots) cleaned &= root.CurrentMatchId.IsEmpty && root.NetworkSession == null && root.NetworkMatches == null && root.NetworkRegions == null;
        SessionState.SetBool(RecordedKey, cleaned);
    }
}

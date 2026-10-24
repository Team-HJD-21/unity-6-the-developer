// 실제 HUD 프리팹의 CU 버튼과 정적·동적 지역 안내를 확인합니다.
// 원격 Client 전송 및 최종 해상도별 디자인 인수는 별도 검사입니다.
using System.Collections;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
public sealed class StageControlUnitHudTests
{
    [Test]
    public void PrefabContainsConfiguredControlUnitView()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab");
        var views = prefab.GetComponentsInChildren<StageControlUnitView>(true);
        Assert.That(views.Length, Is.EqualTo(1));
        Assert.That(views[0].IsConfigured, Is.True);
    }
    [UnityTearDown]
    public IEnumerator StopScene() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    [UnityTest]
    public IEnumerator ActualHud_DisplaysDynamicPolicyAndRequestsAutomaticActivationAtCU()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        var view = Object.FindAnyObjectByType<StageControlUnitView>();
        var binding = Object.FindAnyObjectByType<RegionSceneBinding>();
        var controller = Object.FindAnyObjectByType<FieldTurretController>();
        var cu = Object.FindAnyObjectByType<ControlUnitHealth>();
        var player = Object.FindAnyObjectByType<PlayerInfo>();
        Assert.That(view, Is.Not.Null);
        Assert.That(binding.ResetPolicy(RegionPolicyMode.Dynamic, true), Is.True);
        player.transform.position = cu.transform.position + Vector3.up;
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(view.RegionSummary, Does.Contain("DYNAMIC"));
        Assert.That(view.RegionSummary, Does.Contain("no region locks"));
        var serialized = new SerializedObject(view);
        var button = serialized.FindProperty("autoActivationButton").objectReferenceValue as Button;
        Assert.That(button.interactable, Is.True);
        button.onClick.Invoke();
        Assert.That(controller.AutoActivationCooldownRemaining, Is.GreaterThan(0));
        Assert.That(button.interactable, Is.False);
        yield return new ExitPlayMode();
    }
}

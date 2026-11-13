using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>게임 원본 수치 입력 이후의 표시 반응 검사. 하드웨어 입력 인수와 구분합니다.</summary>
public sealed class CombatHudFeedbackTests
{
    private GameObject _root;
    [Test]
    public void ReapplyingPrefabFeedbackKeepsTrailsAndShadowsBehindTheirSources()
    {
        var style = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath);
        var vitals = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab");
        try
        {
            HudFeedbackReviewSetup.ApplyVitals(vitals, style); HudFeedbackReviewSetup.ApplyVitals(vitals, style);
            foreach (var group in vitals.GetComponentsInChildren<CombatVitalsGroup>(true))
                Assert.That(group.Primary.DamageTrail.transform.GetSiblingIndex(), Is.LessThan(group.Primary.Fill.transform.GetSiblingIndex()),
                    "Repeated setup must not tint a healthy HP bar with its warm damage trail.");
        }
        finally { PrefabUtility.UnloadPrefabContents(vitals); }
        var skill = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        try
        {
            HudFeedbackReviewSetup.ApplySkill(skill, style); HudFeedbackReviewSetup.ApplySkill(skill, style);
            foreach (var wedge in skill.GetComponentInChildren<CombatSkillPicker>(true).Wedges)
                Assert.That(wedge.LiftShadow.transform.GetSiblingIndex(), Is.LessThan(wedge.Art.GetSiblingIndex()));
        }
        finally { PrefabUtility.UnloadPrefabContents(skill); }
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator PlayerAndCuDamageTrailSettlesAndResetIsNotAHit()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseDamage();
    }
    private IEnumerator ExerciseDamage()
    {
        _root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab"));
        _root.GetComponent<InitialStagePlayerHUD>().enabled = false;
        _root.GetComponentInChildren<StagePlayerVitalsView>(true).enabled = false;
        var groups = _root.GetComponentsInChildren<CombatVitalsGroup>(true);
        Assert.That(groups.Length, Is.EqualTo(2));
        foreach (var group in groups)
        {
            group.SetContextVisible(true); group.Clear(); group.SetPrimary(100, 100);
            Assert.That(group.Primary.DamageTrail.fillAmount, Is.EqualTo(1));
        }
        yield return new WaitForSecondsRealtime(.4f);
        foreach (var group in groups) Assert.That(group.Slide.anchoredPosition, Is.EqualTo(Vector2.zero));
        foreach (var group in groups) group.SetPrimary(65, 100);
        yield return null;
        foreach (var group in groups)
        {
            Assert.That(group.Primary.Fill.fillAmount, Is.EqualTo(.65f).Within(.001f));
            Assert.That(group.Primary.DamageTrail.fillAmount, Is.GreaterThan(.65f));
        }
        // A cold Editor frame can outlast the short shake. Its nonzero waveform is also recorded in live Game View QA.
        yield return new WaitForSecondsRealtime(.65f);
        foreach (var group in groups)
        {
            Assert.That(group.Primary.DamageTrail.fillAmount, Is.EqualTo(.65f).Within(.001f));
            Assert.That(group.Slide.anchoredPosition, Is.EqualTo(Vector2.zero));
            group.SetPrimary(30, 50); // A new maximum is rebinding/level data, not damage.
            Assert.That(group.Primary.DamageTrail.fillAmount, Is.EqualTo(.6f).Within(.001f));
            group.Clear();
            Assert.That(group.Primary.DamageTrail.fillAmount, Is.Zero);
        }
    }
    [Test]
    public void WarningOnlyCoversFinalFiveSecondsAndResetsAcrossPhaseAndUnknownTime()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        _root = Object.Instantiate(prefab);
        var header = _root.GetComponentInChildren<CombatHudHeader>(true);
        var apply = typeof(CombatHudHeader).GetMethod("ApplyClock", BindingFlags.NonPublic | BindingFlags.Instance);
        apply.Invoke(header, new object[] { 6, "DAY" });
        Assert.That(header.IsTimeWarning, Is.False);
        foreach (int second in new[] { 5, 4, 3, 2, 1, 0 })
        {
            apply.Invoke(header, new object[] { second, "DAY" });
            Assert.That(header.IsTimeWarning, Is.True);
            Assert.That(header.ClockLabel.color, Is.EqualTo(header.Style.Health));
            Assert.That(header.ClockLabel.rectTransform.localScale.x, Is.GreaterThan(1));
        }
        apply.Invoke(header, new object[] { 300, "NIGHT" });
        Assert.That(header.IsTimeWarning, Is.False);
        Assert.That(header.ClockLabel.color, Is.EqualTo(header.Style.Text));
        Assert.That(header.ClockLabel.rectTransform.localScale, Is.EqualTo(Vector3.one));
        apply.Invoke(header, new object[] { -1, "--" });
        Assert.That(header.ClockLabel.text, Is.EqualTo("-- --:--"));
        Assert.That(header.IsTimeWarning, Is.False);
        Object.DestroyImmediate(_root); _root = null;
    }
}

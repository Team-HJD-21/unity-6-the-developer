using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class MissionUiPresentationTests
{
    private sealed class Empty : IScreenPresenter
    { public void SetFocused(bool focused) { } public void Dispose() { } }
    [Test]
    public void SafetyChoiceCannotBeEscapedAndContextResetReleasesItsInput()
    {
        using var input = new InputLockService();
        using var unrelated = input.Acquire("other");
        using var screens = new ScreenManager(input);
        screens.SetPresenterFactory(_ => new Empty());
        Assert.That(screens.ShowRecovery(), Is.Null);
        screens.BindMatch(new MatchId("mission"));
        var choice = screens.ShowRecovery();
        Assert.That(choice, Is.SameAs(screens.ShowRecovery()));
        Assert.That(screens.Back(), Is.False);
        Assert.That(screens.ShowMenu(), Is.Null);
        var loading = screens.ShowLoading();
        Assert.That(screens.Top.Kind, Is.EqualTo(ScreenKind.Loading));
        screens.BindMatch(new MatchId("replacement"));
        Assert.That(screens.Top, Is.SameAs(loading), "Scene loading retains its own lifetime.");
        screens.Close(loading.Id);
        Assert.That(screens.Count, Is.Zero);
        Assert.That(input.IsBlocked, Is.True);
        unrelated.Dispose(); Assert.That(input.IsBlocked, Is.False);
    }

    [Test]
    public void AuthoredMissionScreensFitTheirTextAndKeepExistingUiAssets()
    {
        var style = Resources.Load<AppScreenStyle>("Core/AppScreenStyle");
        Assert.That(style.HasAuthoredViews, Is.True);
        Assert.That(style.MenuPrefab.MainMenu, Is.Not.Null);
        foreach (var panel in new[] { style.MenuPrefab, style.ResultPrefab, style.RecoveryPrefab })
        {
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (string.IsNullOrEmpty(text.text)) continue;
                Assert.That(text.GetPreferredValues(text.text).x, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), text.name);
                Assert.That(text.text.All(c => char.IsWhiteSpace(c) || text.font.HasCharacter(c, true)), Is.True, text.text);
            }
            var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var button in buttons)
                Assert.That(button.GetComponent<UiButtonFeedback>(), Is.Not.Null);
            for (int i = 0; i < buttons.Length; i++)
            for (int j = i + 1; j < buttons.Length; j++)
            {
                var a = (RectTransform)buttons[i].transform; var b = (RectTransform)buttons[j].transform;
                var ra = new Rect(a.anchoredPosition - Vector2.Scale(a.rect.size, a.pivot), a.rect.size);
                var rb = new Rect(b.anchoredPosition - Vector2.Scale(b.rect.size, b.pivot), b.rect.size);
                Assert.That(ra.Overlaps(rb), Is.False, buttons[i].name + " / " + buttons[j].name);
            }
        }
    }

    [Test]
    public void OneAnnouncementOwnsBothHudTimelinesAndDoesNotBlockInput()
    {
        var skills = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab").GetComponent<CombatHudIntro>();
        var vitals = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab").GetComponent<CombatHudIntro>();
        Assert.That(skills.AnnouncementPrefab, Is.Not.Null);
        Assert.That(vitals.AnnouncementPrefab, Is.Null);
        Assert.That(skills.MessageHoldSeconds, Is.EqualTo(3));
        Assert.That(vitals.AnnouncementSeconds, Is.EqualTo(skills.AnnouncementSeconds));
        Assert.That(skills.AnnouncementPrefab.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Null);
        Assert.That(skills.AnnouncementPrefab.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(g => g.raycastTarget), Is.False);
        Assert.That(skills.AnnouncementPrefab.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("전장에 진입했습니다."));
    }
}

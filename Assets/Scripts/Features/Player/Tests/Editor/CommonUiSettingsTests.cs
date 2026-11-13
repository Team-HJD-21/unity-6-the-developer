using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CommonUiSettingsTests
{
    private const string Key = "CBC.UI.HudScale";
    private GameObject _canvas;
    private SettingsScreenPresenter _presenter;
    private bool _hadPreference;
    private float _oldPreference;

    [TestCase(.5f, .75f)] [TestCase(2f, 1.1f)] [TestCase(.856f, .86f)]
    public void ScaleIsFiniteClampedAndQuantized(float value, float expected)
    {
        var settings = new UiDisplaySettings(false);
        int changed = 0; settings.Changed += _ => changed++;
        settings.SetHudScale(value); settings.SetHudScale(expected);
        Assert.That(settings.HudScale, Is.EqualTo(expected).Within(.0001f));
        Assert.That(changed, Is.EqualTo(1));
        settings.SetHudScale(float.NaN);
        Assert.That(settings.HudScale, Is.EqualTo(UiDisplaySettings.DefaultScale));
    }

    [Test]
    public void SettingsBackReleasesOnlyItsLeaseAndLoadingRejectsSettings()
    {
        using var input = new InputLockService();
        using var unrelated = input.Acquire("unrelated");
        using var screens = new ScreenManager(input);
        screens.SetPresenterFactory(_ => new EmptyPresenter());
        var menu = screens.ShowMenu(); var settings = screens.ShowSettings();
        Assert.That(settings, Is.SameAs(screens.ShowSettings()));
        Assert.That(screens.Count, Is.EqualTo(2));
        Assert.That(screens.Back(), Is.True); Assert.That(screens.Top, Is.SameAs(menu));
        Assert.That(input.IsBlocked, Is.True);
        screens.ShowLoading(); Assert.That(screens.ShowSettings(), Is.Null);
        screens.Dispose(); Assert.That(input.IsBlocked, Is.True);
        unrelated.Dispose(); Assert.That(input.IsBlocked, Is.False);
    }

    [Test]
    public void CommonPrefabsAreAuthoredAndDoNotIncludeFillerInstructions()
    {
        var style = Resources.Load<AppScreenStyle>("Core/AppScreenStyle");
        Assert.That(style.HasAuthoredViews, Is.True);
        Assert.That(style.SettingsPrefab.HudScale.wholeNumbers, Is.True);
        Assert.That(style.SettingsPrefab.HudScale.minValue, Is.EqualTo(75));
        Assert.That(style.SettingsPrefab.HudScale.maxValue, Is.EqualTo(110));
        foreach (var panel in new[] { style.MenuPrefab, style.SettingsPrefab, style.ResultPrefab })
        {
            var opaque = panel.Panel.Find("Opaque Interior").GetComponent<Image>();
            Assert.That(opaque.sprite, Is.Null, "단색 차폐에 투명 여백 스프라이트를 늘리지 않습니다.");
            Assert.That(opaque.color.a, Is.EqualTo(1));
            Assert.That(panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text.Contains("메뉴를 닫") || t.text.Contains("TIP")), Is.False);
        }
    }

    [UnityTest]
    public IEnumerator PreviewCancelApplyAndDefaultUseOneTransaction()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseSettings();
    }

    private IEnumerator ExerciseSettings()
    {
        _hadPreference = PlayerPrefs.HasKey(Key); _oldPreference = PlayerPrefs.GetFloat(Key);
        var style = Resources.Load<AppScreenStyle>("Core/AppScreenStyle");
        _canvas = Object.Instantiate(style.CanvasPrefab).gameObject;
        var parent = _canvas.GetComponent<AppScreenCanvasView>().Screens;
        var settings = new UiDisplaySettings(false);
        int released = 0;
        System.Action close = () => { _presenter.Dispose(); _presenter = null; };
        _presenter = new SettingsScreenPresenter(parent, style.SettingsPrefab, settings, close, () => released++);
        _presenter.SetFocused(true); yield return null;
        var view = _canvas.GetComponentInChildren<AppSettingsView>();
        view.HudScale.value = 110; Assert.That(settings.HudScale, Is.EqualTo(1.1f));
        view.Back.onClick.Invoke();
        Assert.That(settings.HudScale, Is.EqualTo(.85f)); Assert.That(released, Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(.25f);
        _presenter = new SettingsScreenPresenter(parent, style.SettingsPrefab, settings, close, () => released++);
        _presenter.SetFocused(true); yield return null;
        view = _canvas.GetComponentInChildren<AppSettingsView>();
        view.HudScale.value = 75; view.Apply.onClick.Invoke();
        Assert.That(settings.HudScale, Is.EqualTo(.75f));
        Assert.That(new UiDisplaySettings().HudScale, Is.EqualTo(.75f));
        yield return new WaitForSecondsRealtime(.25f);
        _presenter = new SettingsScreenPresenter(parent, style.SettingsPrefab, settings, close, () => released++);
        _presenter.SetFocused(true); yield return null;
        view = _canvas.GetComponentInChildren<AppSettingsView>(); view.Defaults.onClick.Invoke();
        Assert.That(settings.HudScale, Is.EqualTo(.85f)); view.Back.onClick.Invoke();
        Assert.That(settings.HudScale, Is.EqualTo(.75f)); Assert.That(released, Is.EqualTo(3));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        _presenter?.Dispose(); _presenter = null;
        if (_canvas != null) Object.DestroyImmediate(_canvas);
        if (Application.isPlaying)
        {
            if (_hadPreference) PlayerPrefs.SetFloat(Key, _oldPreference); else PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save(); yield return new ExitPlayMode();
        }
    }
    private sealed class EmptyPresenter : IScreenPresenter
    { public void SetFocused(bool focused) { } public void Dispose() { } }
}

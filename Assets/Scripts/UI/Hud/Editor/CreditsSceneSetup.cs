using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>기존 Credits 씬을 메인 메뉴에 연결합니다. Credits 연출은 제작된 프리팹과 Timeline에서 편집합니다.</summary>
public static class CreditsSceneSetup
{
    public const string Prefab = "Assets/Prefabs/UI/Cinematics/CreditsCinematic.prefab";

    public static void ConnectMain(Scene scene)
    {
        var controls = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<StartupControlsView>(true)).Single();
        var list = controls.Content;
        var credits = list.Find("Credits")?.GetComponent<Button>();
        if (credits == null)
        {
            credits = Object.Instantiate(controls.Settings, list, false);
            credits.name = "Credits";
            credits.GetComponent<UiButtonFeedback>().Label.text = "CREDITS";
        }

        var buttons = new[] { controls.Start, controls.Settings, credits, controls.Exit };
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            ((RectTransform)button.transform).anchoredPosition = new Vector2(0, 160 - i * 110);
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                selectOnDown = buttons[(i + 1) % buttons.Length]
            };
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button.transform);
        }

        var startup = scene.GetRootGameObjects().Select(root => root.GetComponent<StartupSceneRoot>()).Single(root => root != null);
        var launcher = startup.GetComponent<CreditsSceneLauncher>() ?? startup.gameObject.AddComponent<CreditsSceneLauncher>();
        launcher.Button = credits;
        launcher.MenuCanvases = scene.GetRootGameObjects().Select(root => root.GetComponent<Canvas>())
            .Where(canvas => canvas != null && canvas.name != "Credits Transition").ToArray();

        var existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Credits Transition");
        if (existing != null) Object.DestroyImmediate(existing);
        var transition = new GameObject("Credits Transition", typeof(RectTransform));
        transition.layer = 30;
        var outer = transition.GetComponent<RectTransform>();
        outer.anchorMin = outer.anchorMax = outer.pivot = Vector2.one * .5f;
        outer.sizeDelta = new Vector2(1920, 1080);
        SceneManager.MoveGameObjectToScene(transition, scene);
        var canvas = transition.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = transition.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var fill = new GameObject("Black", typeof(RectTransform), typeof(Image));
        fill.layer = 30;
        var rect = fill.GetComponent<RectTransform>();
        rect.SetParent(transition.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = Color.black;
        transition.AddComponent<GraphicRaycaster>();
        launcher.Transition = transition.AddComponent<CanvasGroup>();
        launcher.Transition.alpha = 0;
        launcher.Transition.blocksRaycasts = false;

        startup.ConfigureCredits(launcher);
        EditorUtility.SetDirty(startup);
        EditorUtility.SetDirty(launcher);
        FrontendSoundtrackSetup.ConfigureMenu(scene);
    }
}

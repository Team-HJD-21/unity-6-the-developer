// 공통 UI는 여기서 제작하고, 실행 중에는 직렬화된 참조와 프리팹 인스턴스만 사용합니다.
using System;
using System.IO;
using System.Linq;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CommonUiPrefabSetup
{
    public const string Folder = "Assets/Prefabs/UI/Common";
    private static CombatHudStyle S;
    private static GameObject ButtonAsset, TextButtonAsset;
    private static readonly Color Face = new(.11f, .14f, .15f, 1f);

    [MenuItem("Tools/TeamHJD/The Developer/Common UI/Refresh main controls")]
    public static void ConfigureMainControls()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        S = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath);
        TextButtonAsset = Save("MenuTextButton", r => BuildButton(r, true));
        Save("StartupControls", BuildStartup);
        AssetDatabase.SaveAssets(); ConfigureStartupScene(); AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/TeamHJD/The Developer/Common UI/Build authored views")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        S = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath);
        if (S == null) throw new InvalidOperationException("Import the combat HUD style first.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        ButtonAsset = Save("CommonButton", r => BuildButton(r, false));
        TextButtonAsset = Save("MenuTextButton", r => BuildButton(r, true));
        var canvas = Save("AppScreenCanvas", BuildCanvas).GetComponent<AppScreenCanvasView>();
        var menu = Save("MenuScreen", BuildMenu).GetComponent<AppPanelView>();
        var result = Save("ResultScreen", BuildResult).GetComponent<AppPanelView>();
        var settings = Save("SettingsScreen", BuildSettings).GetComponent<AppSettingsView>();
        var loading = Save("LoadingScreen", BuildLoading).GetComponent<AppLoadingView>();
        Save("StartupControls", BuildStartup);
        var style = AssetDatabase.LoadAssetAtPath<AppScreenStyle>("Assets/Resources/Core/AppScreenStyle.asset");
        style.CanvasPrefab = canvas; style.MenuPrefab = menu; style.ResultPrefab = result;
        style.SettingsPrefab = settings; style.LoadingPrefab = loading; EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets(); ConfigureStartupScene(); AssetDatabase.SaveAssets();
        Debug.Log("COMMON_UI_PREFABS_CONFIGURED");
    }

    private static GameObject Save(string name, Action<RectTransform> build)
    {
        string path = Folder + "/" + name + ".prefab";
        bool exists = File.Exists(path);
        var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name, typeof(RectTransform));
        try { build((RectTransform)root.transform); return PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }
    private static void BuildButton(RectTransform root, bool textOnly)
    {
        Size(root, textOnly ? 420 : 406, textOnly ? 66 : 62);
        var hit = Get<Image>(root); hit.sprite = S.Get("fill_white"); hit.color = new Color(1, 1, 1, .001f); hit.raycastTarget = true;
        var button = Get<Button>(root); button.targetGraphic = hit; button.transition = Selectable.Transition.None;
        Image edge = null;
        if (!textOnly) { Panel(root, "button", false); edge = root.Find("Edge").GetComponent<Image>(); }
        var label = Label("Label", root, textOnly ? "START GAME" : "계속하기", root.rect.width - 36, 48, 0, 0,
            textOnly ? 30 : 23, textOnly ? S.LabelFont : S.BodyFont);
        Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(18, 7); label.rectTransform.offsetMax = new Vector2(-18, -7);
        var mark = Rect("Focus Mark", root, 0, 0); Stretch(mark); var group = Get<CanvasGroup>(mark); group.alpha = 0; group.blocksRaycasts = false;
        mark.gameObject.SetActive(!textOnly);
        var feedback = Get<UiButtonFeedback>(root); feedback.Label = label; feedback.Edge = edge;
        feedback.IdleEdge = textOnly ? null : S.Get("button_edge_idle"); feedback.HoverEdge = textOnly ? null : S.Get("button_edge_hover");
        feedback.FocusMark = textOnly ? null : group; feedback.Text = S.Text; feedback.Accent = S.Accent;
    }
    private static void BuildCanvas(RectTransform root)
    {
        Canvas(root, 10000);
        var view = Get<AppScreenCanvasView>(root);
        view.Screens = Rect("Screen Stack", root, 0, 0); Stretch(view.Screens);
        var fade = Rect("Scene Travel Fade", root, 0, 0); Stretch(fade);
        var image = Get<Image>(fade); image.sprite = null; image.color = Color.black; image.raycastTarget = true;
        view.TravelFade = Get<CanvasGroup>(fade); view.TravelFade.alpha = 0; fade.gameObject.SetActive(false);
    }
    private static void BuildMenu(RectTransform root)
    {
        var view = Overlay<AppPanelView>(root, 510, 410);
        view.SecondaryRowHeight = 80;
        view.Title = TopLabel("Title", view.Panel, "MENU", 42, 28, 426, 50, 32, S.HeaderFont);
        TopRule(view.Panel, 42, 89, 426);
        view.Primary = Button(view.Panel, "Resume", "계속하기", 52, 123, 406, 62);
        view.Settings = Button(view.Panel, "Settings", "설정", 52, 203, 406, 62);
        view.Secondary = Button(view.Panel, "Return", "우주선으로", 52, 283, 406, 62);
        view.Status = TopLabel("Action Feedback", view.Panel, "", 42, 354, 426, 34, 16, S.BodyFont);
        view.Status.textWrappingMode = TextWrappingModes.Normal; view.Body = null;
        Navigate(view.Primary, view.Settings, view.Secondary);
    }
    private static void BuildResult(RectTransform root)
    {
        var view = Overlay<AppPanelView>(root, 720, 480);
        view.Title = TopLabel("Title", view.Panel, "MATCH COMPLETE", 42, 30, 636, 54, 32, S.HeaderFont);
        TopRule(view.Panel, 42, 98, 636);
        view.Body = TopLabel("Result", view.Panel, "", 42, 124, 636, 116, 24, S.BodyFont);
        view.Body.textWrappingMode = TextWrappingModes.Normal;
        view.Status = TopLabel("Action Feedback", view.Panel, "", 42, 262, 636, 58, 18, S.BodyFont);
        view.Status.textWrappingMode = TextWrappingModes.Normal;
        view.Primary = Button(view.Panel, "Restart", "다시 시작", 42, 362, 294, 62);
        view.Secondary = Button(view.Panel, "Return", "우주선으로", 384, 362, 294, 62);
        view.Settings = null; Navigate(view.Primary, view.Secondary);
    }
    private static void BuildSettings(RectTransform root)
    {
        var view = Overlay<AppSettingsView>(root, 840, 500);
        view.Title = TopLabel("Title", view.Panel, "SETTINGS", 42, 26, 756, 54, 32, S.HeaderFont);
        TopRule(view.Panel, 42, 90, 756);
        TopLabel("HUD Scale Label", view.Panel, "전투 HUD 크기", 42, 126, 290, 40, 22, S.BodyFont);
        view.ScaleValue = TopLabel("HUD Scale Value", view.Panel, "85%", 340, 126, 134, 40, 28, S.LabelFont);
        view.ScaleValue.alignment = TextAlignmentOptions.MidlineRight; view.ScaleValue.color = S.Accent;
        var slider = Rect("HUD Scale Slider", view.Panel, 432, 38); Top(slider, 42, 178);
        var hit = Get<Image>(slider); hit.sprite = S.Get("fill_white"); hit.color = Color.clear; hit.raycastTarget = true;
        ImageFill("Track", slider, 432, 4, 0, 0, new Color(.28f, .31f, .29f));
        var fillArea = Rect("Fill Area", slider, 0, 0); Stretch(fillArea); fillArea.offsetMin = new Vector2(10, 17); fillArea.offsetMax = new Vector2(-10, -17);
        var fill = ImageFill("Fill", fillArea, 0, 0, 0, 0, S.Accent); Stretch(fill.rectTransform);
        var handleArea = Rect("Handle Area", slider, 0, 0); Stretch(handleArea); handleArea.offsetMin = Vector2.right * 10; handleArea.offsetMax = Vector2.left * 10;
        var handle = Picture("Handle", handleArea, "effect_surface", 20, 28, 0, 0); handle.raycastTarget = true;
        Stretch(Picture("Edge", handle.transform, "effect_edge_hover", 20, 28, 0, 0).rectTransform);
        ImageFill("Grip", handle.transform, 2, 14, 0, 0, S.Accent);
        view.HudScale = Get<Slider>(slider); view.HudScale.fillRect = fill.rectTransform; view.HudScale.handleRect = handle.rectTransform; view.HudScale.targetGraphic = handle;
        view.HudScale.minValue = UiDisplaySettings.MinimumScale * 100; view.HudScale.maxValue = UiDisplaySettings.MaximumScale * 100;
        view.HudScale.wholeNumbers = true; view.HudScale.SetValueWithoutNotify(UiDisplaySettings.DefaultScale * 100);
        TopLabel("Minimum", view.Panel, "75%", 42, 222, 100, 28, 16, S.LabelFont).color = new Color(.58f, .62f, .59f);
        var max = TopLabel("Maximum", view.Panel, "110%", 374, 222, 100, 28, 16, S.LabelFont); max.alignment = TextAlignmentOptions.MidlineRight; max.color = new Color(.58f, .62f, .59f);
        var caption = TopLabel("Preview Label", view.Panel, "HUD 미리보기", 549, 118, 242, 32, 18, S.BodyFont); caption.alignment = TextAlignmentOptions.Center;
        view.Preview = Rect("HUD Preview", view.Panel, 242, 230); view.Preview.anchorMin = view.Preview.anchorMax = new Vector2(.5f, .5f); view.Preview.anchoredPosition = new Vector2(250, 0);
        var weapon = Rect("Weapon", view.Preview, 242, 70); weapon.anchoredPosition = new Vector2(0, 65); Panel(weapon, "weapon", false);
        Picture("Original Weapon", weapon, "weapon_default", 172, 62, 22, 0).preserveAspect = true;
        Label("Key", weapon, "2", 36, 44, -100, 0, 25, S.LabelFont);
        for (int i = 0; i < 3; i++)
        {
            float width = i == 2 ? 102 : 56;
            var tile = Rect("Skill " + i, view.Preview, width, 82); tile.anchoredPosition = new Vector2(-93 + i * 66 + (i == 2 ? 23 : 0), -60); Panel(tile, "skill", false);
            Label("Key", tile, new[] { "Q", "E", "SPACE" }[i], width - 6, 30, 0, -16, i == 2 ? 14 : 21, S.LabelFont);
        }
        view.Defaults = Button(view.Panel, "Defaults", "기본값", 42, 418, 180, 54);
        view.Back = Button(view.Panel, "Cancel", "취소", 412, 418, 180, 54);
        view.Apply = Button(view.Panel, "Apply", "적용", 610, 418, 180, 54);
        view.Primary = view.Apply; view.Secondary = view.Back; view.Settings = null; view.Body = view.Status = null;
        Navigate(view.HudScale, view.Defaults, view.Back, view.Apply);
        HudUiReviewSetup.ApplySettingsPreview(root.gameObject, S);
    }
    private static void BuildLoading(RectTransform root)
    {
        Stretch(root); var bg = Get<Image>(root); bg.sprite = null; bg.color = new Color(.063f, .09f, .098f); bg.raycastTarget = true;
        var view = Get<AppLoadingView>(root);
        var content = Rect("Content", root, 1920, 1080);
        TopLabel("Brand", content, "THE DEVELOPER", 120, 96, 900, 48, 30, S.HeaderFont);
        TopLabel("Loading", content, "LOADING", 300, 232, 1320, 58, 38, S.HeaderFont);
        var panel = Rect("Controls", content, 1320, 390); Top(panel, 300, 310); Panel(panel, "panel", true);
        TopLabel("Title", panel, "조작 안내", 48, 22, 1200, 46, 24, S.BodyFont); TopRule(panel, 48, 81, 1224);
        string[] letters = { "W", "A", "S", "D" };
        for (int i = 0; i < 4; i++) Key(panel, letters[i], 70 + i * 54, 123, 44);
        TopLabel("Move", panel, "이동", 320, 123, 250, 42, 22, S.BodyFont);
        Key(panel, "LMB", 710, 123, 78); TopLabel("Attack", panel, "공격", 816, 123, 250, 42, 22, S.BodyFont);
        Key(panel, "Q", 70, 224, 44); Key(panel, "E", 124, 224, 44); TopLabel("Skills", panel, "장착한 스킬", 205, 224, 300, 42, 22, S.BodyFont);
        Key(panel, "SPACE", 710, 224, 112); TopLabel("Dash", panel, "대시", 850, 224, 250, 42, 22, S.BodyFont);
        var track = ImageFill("Progress Track", content, 1320, 4, 0, 0, new Color(.24f, .28f, .26f)); Top(track.rectTransform, 300, 902);
        var fill = ImageFill("Progress Fill", track.transform, 0, 0, 0, 0, S.Accent); Stretch(fill.rectTransform); view.ProgressFill = fill.rectTransform;
        view.ProgressLabel = TopLabel("Actual Progress", content, "", 1200, 850, 420, 42, 24, S.LabelFont); view.ProgressLabel.alignment = TextAlignmentOptions.MidlineRight;
    }
    private static void BuildStartup(RectTransform root)
    {
        Canvas(root, 1); var view = Get<StartupControlsView>(root);
        view.Group = Get<CanvasGroup>(root);
        view.Content = Rect("Button List", root, 420, 420); view.Content.anchoredPosition = new Vector2(0, -220);
        foreach (string name in new[] { "Start", "Settings", "Credits", "Exit" })
        { var old = root.Find(name); if (old != null) old.SetParent(view.Content, false); }
        view.Start = MenuButton(view.Content, "Start", "START GAME", 160, 30);
        view.Settings = MenuButton(view.Content, "Settings", "SETTINGS", 50, 30);
        var credits = MenuButton(view.Content, "Credits", "CREDITS", -60, 30);
        view.Exit = MenuButton(view.Content, "Exit", "EXIT", -170, 30);
        Navigate(view.Start, view.Settings, credits, view.Exit);
        var oldVersion = root.Find("Version"); if (oldVersion != null) oldVersion.gameObject.SetActive(false);
    }
    private static void ConfigureStartupScene()
    {
        const string path = "Assets/Scenes/Bootstrap/Startup.unity";
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Preserve the open dirty Scene before authoring Startup.");
        try
        {
            var scene = EditorSceneManager.OpenScene(path);
            // Scene 교체 뒤에는 제작 중의 임시 참조 대신 저장한 prefab asset을 다시 읽습니다.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/StartupControls.prefab");
            var canvas = scene.GetRootGameObjects().Single(r => r.name == "Canvas");
            foreach (string name in new[] { "ButtonSet", "VariousSet" })
            { var old = canvas.transform.Find(name); if (old != null) old.gameObject.SetActive(false); }
            var existing = scene.GetRootGameObjects().Select(r => r.GetComponent<StartupControlsView>()).FirstOrDefault(v => v != null);
            if (existing == null) existing = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<StartupControlsView>();
            existing.name = "Startup Controls";
            existing.IntroLogo = canvas.transform.Find("LogoSet").GetComponent<Animator>(); EditorUtility.SetDirty(existing);
            ConfigureStartupVersion(scene);
            var presenter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StartMenuPresenter>(true)).Single();
            presenter.Configure(existing.Start, existing.Exit, existing.Settings); EditorUtility.SetDirty(presenter);
            if (File.Exists(CreditsSceneLauncher.ScenePath)) CreditsSceneSetup.ConnectMain(scene);
            if (File.Exists(StartupAtmosphereSetup.Art+"/MeteorSheet.png")) StartupAtmosphereSetup.Configure(scene);
            foreach (var events in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)))
            {
                var legacy = events.GetComponent<StandaloneInputModule>(); if (legacy != null) Object.DestroyImmediate(legacy);
                var module = Get<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(events); module.AssignDefaultActions();
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    public static void ConfigureStartupVersion(UnityEngine.SceneManagement.Scene scene)
    {
        var canvas=scene.GetRootGameObjects().Single(r=>r.name=="Canvas");
        var label=canvas.transform.Find("Version").GetComponent<TMPro.TMP_Text>();
        label.gameObject.SetActive(true);label.text="v"+Application.version.TrimStart('v','V');
        label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/JetBrainsMono/JetBrainsMono-Medium SDF.asset");
        label.fontSize=18;label.enableAutoSizing=false;label.color=new Color(.72f,.76f,.75f);label.raycastTarget=false;
        label.alignment=TextAlignmentOptions.BottomLeft;
        var rect=label.rectTransform;rect.localScale=Vector3.one;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;
        rect.anchoredPosition=new Vector2(48,24);rect.sizeDelta=new Vector2(360,32);
        var fit=label.GetComponent<ContentSizeFitter>();if(fit!=null)fit.enabled=false;
        var view=scene.GetRootGameObjects().Select(r=>r.GetComponent<StartupControlsView>()).Single(v=>v!=null);
        view.VersionLabel=label;EditorUtility.SetDirty(view);EditorUtility.SetDirty(label);EditorUtility.SetDirty(rect);
        PrefabUtility.RecordPrefabInstancePropertyModifications(view);
        if(fit!=null)EditorUtility.SetDirty(fit);
    }

    private static T Overlay<T>(RectTransform root, float w, float h) where T : AppPanelView
    {
        Stretch(root); var image = Get<Image>(root); image.sprite = null; image.color = new Color(.02f, .035f, .04f, .67f); image.raycastTarget = true;
        var view = Get<T>(root); view.Group = Get<CanvasGroup>(root); view.Group.alpha = 1;
        view.Panel = Rect("Panel", root, w, h); view.Panel.anchoredPosition = Vector2.zero; Panel(view.Panel, "panel", true); return view;
    }
    private static void Canvas(RectTransform root, int sorting)
    {
        var canvas = Get<Canvas>(root); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sorting;
        var scaler = Get<CanvasScaler>(root); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; Get<GraphicRaycaster>(root);
    }
    private static void Panel(RectTransform parent, string kind, bool backing)
    {
        if (backing) { var b = ImageFill("Opaque Interior", parent, 0, 0, 0, 0, Face); Stretch(b.rectTransform); b.rectTransform.offsetMin = new Vector2(7, 7); b.rectTransform.offsetMax = new Vector2(-7, -7); }
        Stretch(Picture("Surface", parent, kind + "_surface", 0, 0, 0, 0).rectTransform);
        Stretch(Picture("Edge", parent, kind + "_edge_idle", 0, 0, 0, 0).rectTransform);
    }
    private static Button Button(Transform parent, string name, string label, float x, float y, float w, float h)
    {
        var found = parent.Find(name);
        var root = found != null ? found.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(ButtonAsset, parent);
        root.name = name; var rect = (RectTransform)root.transform; Size(rect, w, h); Top(rect, x, y);
        var feedback = root.GetComponent<UiButtonFeedback>(); feedback.Label.text = label;
        return root.GetComponent<Button>();
    }
    private static Button MenuButton(Transform parent, string name, string label, float y, float size)
    {
        var found = parent.Find(name);
        var root = found != null ? found.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(TextButtonAsset, parent);
        root.name = name; var rect = (RectTransform)root.transform; Size(rect, 420, 66); rect.anchoredPosition = new Vector2(0, y);
        var feedback = root.GetComponent<UiButtonFeedback>(); feedback.Label.text = label; feedback.Label.fontSize = size; return root.GetComponent<Button>();
    }
    private static void Navigate(params Selectable[] controls)
    {
        for (int i = 0; i < controls.Length; i++) controls[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = controls[(i + controls.Length - 1) % controls.Length], selectOnDown = controls[(i + 1) % controls.Length] };
    }
    private static void Key(Transform parent, string name, float x, float y, float width)
    {
        var root = Rect("Key " + name, parent, width, 42); Top(root, x, y); Panel(root, "effect", false);
        Label("Label", root, name, width - 4, 38, 0, 0, 19, S.LabelFont);
    }
    private static T Get<T>(Component c) where T : Component { var v = c.GetComponent<T>(); return v != null ? v : c.gameObject.AddComponent<T>(); }
    private static RectTransform Rect(string name, Transform parent, float w, float h)
    {
        var r = parent.Find(name) as RectTransform;
        if (r == null) { r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); }
        Size(r, w, h); return r;
    }
    private static void Size(RectTransform r, float w, float h)
    { r.gameObject.layer = 5; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(w, h); r.localScale = Vector3.one; r.localRotation = Quaternion.identity; }
    private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    private static void Top(RectTransform r, float x, float y) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); }
    private static Image Picture(string name, Transform parent, string id, float w, float h, float x, float y)
    {
        var r = Rect(name, parent, w, h); r.anchoredPosition = new Vector2(x, y); var image = Get<Image>(r); image.sprite = S.Get(id);
        image.color = Color.white; image.raycastTarget = false; image.type = image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple; return image;
    }
    private static Image ImageFill(string name, Transform parent, float w, float h, float x, float y, Color color)
    { var image = Picture(name, parent, "fill_white", w, h, x, y); image.sprite = null; image.type = Image.Type.Simple; image.color = color; return image; }
    private static TMP_Text Label(string name, Transform parent, string value, float w, float h, float x, float y, float size, TMP_FontAsset font)
    {
        var r = Rect(name, parent, w, h); r.anchoredPosition = new Vector2(x, y); var text = Get<TextMeshProUGUI>(r);
        text.text = value; text.font = font; text.fontSize = size; text.color = S.Text; text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false; return text;
    }
    private static TMP_Text TopLabel(string name, Transform parent, string value, float x, float y, float w, float h, float size, TMP_FontAsset font)
    { var text = Label(name, parent, value, w, h, 0, 0, size, font); Top(text.rectTransform, x, y); text.alignment = TextAlignmentOptions.MidlineLeft; return text; }
    private static void TopRule(Transform parent, float x, float y, float width)
    { var line = ImageFill("Rule", parent, width, 1, 0, 0, new Color(.47f, .51f, .48f)); Top(line.rectTransform, x, y); }
}

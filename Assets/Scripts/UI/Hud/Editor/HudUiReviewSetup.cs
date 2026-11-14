using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 제작 계층 중 리뷰 대상만 수정합니다. 다른 패널의 Inspector 조정은 보존합니다.</summary>
public static class HudUiReviewSetup
{
    [MenuItem("Tools/TeamHJD/The Developer/HUD v6/Apply review refinements")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var style = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath);
        Patch("Assets/Prefabs/UI/PlayerSkillHUD.prefab", root => ApplySkill(root, style));
        Patch("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab", root => ApplyVitals(root, style));
        Patch("Assets/Prefabs/UI/Common/SettingsScreen.prefab", root => ApplySettingsPreview(root, style));
        CommonUiPrefabSetup.ConfigureMainControls(); AssetDatabase.SaveAssets();
    }
    private static void Patch(string path, Action<GameObject> apply)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void ApplySkill(GameObject root, CombatHudStyle style)
    {
        var dash = root.GetComponentInChildren<PlayerDashHud>(true).transform.Find("Dash").GetComponent<Image>();
        dash.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/PlayerDashAfterimageIcon.png");
        dash.preserveAspect = true; dash.rectTransform.sizeDelta = new Vector2(90, 68); dash.enabled = true;
        var ammo = root.GetComponentInChildren<CombatPlayerIndicators>(true);
        Symbol(ammo.ReloadArrows, CombatHudSymbol.Shape.Reload, style.Text, 1.8f);
        var header = root.GetComponentInChildren<CombatHudHeader>(true);
        ((RectTransform)header.transform).sizeDelta = new Vector2(430, 56);
        Text(header.ClockLabel, new Vector2(220, 40), new Vector2(-88, -28), 24);
        Text(header.OccupancyLabel, new Vector2(90, 40), new Vector2(156, -28), 24);
        Place((RectTransform)header.transform.Find("Territory Lettering"), 48, 28, 77, -28);
        var territory = header.transform.Find("Territory Lettering");
        territory.GetComponent<Image>().enabled = false;
        var lettering = Get<TextMeshProUGUI>(Rect("Text", territory, 48, 28));
        lettering.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Mulmaru SDF.asset");
        if (lettering.font == null) throw new InvalidOperationException("Pull the Mulmaru font assets first.");
        if ((!lettering.font.HasCharacter(0xC810) || !lettering.font.HasCharacter(0xC720)) &&
            !lettering.font.TryAddCharacters("점유", out string missing))
            throw new InvalidOperationException("Mulmaru glyphs unavailable: " + missing);
        lettering.text = "점유"; lettering.fontSize = 22; lettering.color = style.Text;
        lettering.alignment = TextAlignmentOptions.Center; lettering.textWrappingMode = TextWrappingModes.NoWrap;
        lettering.raycastTarget = false;
        Place((RectTransform)header.transform.Find("Separator"), 1, 22, 32, -28);
        Place((RectTransform)header.transform.Find("Rule"), 392, 1, 5, -51);
        header.SlowCount.fontSize = 16; header.SlowDescription.fontSize = 15;
        root.GetComponentInChildren<CombatBossView>(true).Name.fontSize = 21;
        var stack = root.GetComponentInChildren<CombatWeaponStack>(true);
        stack.NumberButtons = new Button[4]; stack.NumberLabels = new TMP_Text[4];
        var strip = Rect("Weapon Numbers", stack.transform, 34, 104);
        strip.anchorMin = strip.anchorMax = strip.pivot = Vector2.zero; strip.anchoredPosition = new Vector2(361, 58);
        for (int i = 0; i < 4; i++)
        {
            var slot = stack.Slots[i];
            slot.transform.Find("V6 Key").GetComponent<TMP_Text>().enabled = false;
            slot.transform.Find("V6 Selection").GetComponent<Image>().enabled = false;
            stack.Groups[i].alpha = i == 1 ? 1 : 0;
            stack.Groups[i].interactable = stack.Groups[i].blocksRaycasts = i == 1;
            ((RectTransform)slot.transform).anchoredPosition = new Vector2(57, 68);
            var rect = Rect("Slot " + (i + 1), strip, 34, 26);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -13 - 26 * i);
            var hit = Get<Image>(rect); hit.sprite = null; hit.color = new Color(1, 1, 1, .001f); hit.raycastTarget = true;
            var button = Get<Button>(rect); button.targetGraphic = hit; button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var labelRect = Rect("Number", rect, 34, 26);
            var label = Get<TextMeshProUGUI>(labelRect); label.text = (i + 1).ToString(); label.font = style.LabelFont;
            label.fontSize = i == 1 ? 25 : 17; label.color = i == 1 ? style.Accent : new Color(.46f, .49f, .46f, 1);
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            stack.NumberButtons[i] = button; stack.NumberLabels[i] = label;
        }
        strip.SetAsLastSibling();
        ApplyIntro(root, style, root.GetComponent<PlayerSkillHud>(), null);
        HudPointerReviewSetup.Apply(root, style);
        HudFeedbackReviewSetup.ApplySkill(root, style);
        HudAmmoDockSetup.Apply(root, style);
    }
    public static void ApplyVitals(GameObject root, CombatHudStyle style)
    {
        foreach (var group in root.GetComponentsInChildren<CombatVitalsGroup>(true))
        {
            group.StartPinned = true;
            var pin = (RectTransform)group.Pin.transform;
            pin.anchorMin = pin.anchorMax = new Vector2(group.RightEdge ? 1 : 0, 1);
            pin.anchoredPosition = new Vector2(group.RightEdge ? -84 : 75, -50);
            Symbol((RectTransform)group.Pin.transform, CombatHudSymbol.Shape.Lock, style.Text, 1.6f);
        }
        ApplyIntro(root, style, null, root.GetComponent<InitialStagePlayerHUD>());
        HudFeedbackReviewSetup.ApplyVitals(root, style);
        var turret = root.GetComponentInChildren<StageTurretControlView>(true);
        if(turret==null)return;
        var serialized = new SerializedObject(turret);
        var activation = (Button)serialized.FindProperty("activationButton").objectReferenceValue;
        activation.GetComponent<TeamHJD.Game.Presentation.UiButtonFeedback>().PreserveLabelColor = true;
        var close = (Button)serialized.FindProperty("closeButton").objectReferenceValue;
        ((RectTransform)close.transform).sizeDelta = new Vector2(70, 40);
        foreach (string branch in new[] { "LowPower", "HighFirepower" })
        {
            var label = turret.transform.Find(branch + "Text").GetComponent<TMP_Text>();
            Text(label, new Vector2(132, 28), new Vector2(200, branch == "LowPower" ? 24 : -24), 15);
            foreach (string action in new[] { "Minus", "Plus" })
                Place((RectTransform)turret.transform.Find(branch + action), 40, 40, action == "Minus" ? 293 : 341, branch == "LowPower" ? 24 : -24);
        }
    }
    private static void ApplyIntro(GameObject root, CombatHudStyle style, PlayerSkillHud skill, InitialStagePlayerHUD stage)
    {
        const string path = "Assets/Art/UI/HudV6/HudBootReveal.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("The Developer/UI/HudBootReveal");
            if (shader == null) throw new InvalidOperationException("Import HudBootReveal.shader first.");
            material = new Material(shader) { name = "HudBootReveal" }; AssetDatabase.CreateAsset(material, path);
        }
        var graphic = Get<Image>(root.transform); graphic.sprite = null; graphic.color = Color.white;
        graphic.raycastTarget = false; graphic.material = material;
        var mask = Get<Mask>(root.transform); mask.showMaskGraphic = false; mask.enabled = false;
        var scanRect = Rect("HUD Boot Scan", root.transform, 0, 0);
        scanRect.anchorMin = Vector2.zero; scanRect.anchorMax = Vector2.one; scanRect.offsetMin = scanRect.offsetMax = Vector2.zero;
        var scan = Get<Image>(scanRect); scan.sprite = null; scan.material = material;
        scan.color = new Color(style.Text.r, style.Text.g, style.Text.b, .22f); scan.raycastTarget = false; scan.enabled = false;
        scanRect.SetAsLastSibling();
        var intro = Get<CombatHudIntro>(root.transform); intro.Group = Get<CanvasGroup>(root.transform);
        intro.RevealMask = mask; intro.MaskGraphic = graphic; intro.ScanGraphic = scan; intro.MaterialTemplate = material;
        intro.SkillHud = skill; intro.Stage = stage; graphic.enabled = false;
    }
    public static void ApplySettingsPreview(GameObject root, CombatHudStyle style)
    {
        var preview = root.GetComponent<TeamHJD.Game.Presentation.AppSettingsView>().Preview;
        var weapon = preview.Find("Weapon");
        ((RectTransform)weapon).sizeDelta = new Vector2(270,70);
        var oldKey = weapon.Find("Key"); if (oldKey != null) oldKey.gameObject.SetActive(false);
        var gun=weapon.Find("Original Weapon").GetComponent<RectTransform>();
        gun.anchoredPosition = new Vector2(-33,0); gun.sizeDelta=new Vector2(170,58);
        var authoredPlayer=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        int capacity=authoredPlayer.GetComponent<PlayerWeaponAmmo>().GetCapacity(WeaponType.DefaultGun);
        var current=Get<TextMeshProUGUI>(Rect("Ammo Current",weapon,62,32));
        current.rectTransform.anchoredPosition=new Vector2(91,7);current.font=style.LabelFont;current.text=Mathf.Max(0,capacity-3).ToString();current.fontSize=25;current.color=style.Text;current.alignment=TextAlignmentOptions.Center;current.raycastTarget=false;
        current.textWrappingMode=TextWrappingModes.NoWrap;current.overflowMode=TextOverflowModes.Overflow;current.enableAutoSizing=false;
        var reserve=Get<TextMeshProUGUI>(Rect("Ammo Maximum",weapon,62,20));
        reserve.rectTransform.anchoredPosition=new Vector2(91,-19);reserve.font=style.LabelFont;reserve.text="/ "+capacity;reserve.fontSize=13;reserve.color=new Color(style.Text.r,style.Text.g,style.Text.b,.55f);reserve.alignment=TextAlignmentOptions.Center;reserve.raycastTarget=false;
        reserve.textWrappingMode=TextWrappingModes.NoWrap;reserve.overflowMode=TextOverflowModes.Overflow;reserve.enableAutoSizing=false;
        for (int i = 0; i < 4; i++)
        {
            var rect = Rect("Weapon Number " + (i + 1), preview, 24, 22);
            rect.anchoredPosition = ((RectTransform)weapon).anchoredPosition + new Vector2(-154,30 - 20 * i);
            var label = Get<TextMeshProUGUI>(rect); label.text = (i + 1).ToString(); label.font = style.LabelFont;
            label.fontSize = i == 1 ? 20 : 13; label.color = i == 1 ? style.Accent : new Color(.46f, .49f, .46f, 1);
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        }
        var space = preview.Find("Skill 2");
        var art = Get<Image>(Rect("Dash", space, 64, 40)); art.rectTransform.anchoredPosition = new Vector2(0, 16);
        art.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/PlayerDashAfterimageIcon.png"); art.preserveAspect = true; art.raycastTarget = false;
        space.Find("Key").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -24);
    }
    private static void Symbol(RectTransform root, CombatHudSymbol.Shape shape, Color color, float stroke)
    {
        var old = root.GetComponent<Image>(); if (old != null) old.enabled = false;
        var rect = Rect("Clean Symbol", root, root.rect.width, root.rect.height);
        Get<CanvasRenderer>(rect);
        var symbol = Get<CombatHudSymbol>(rect); symbol.Symbol = shape; symbol.color = color; symbol.Stroke = stroke; symbol.raycastTarget = false;
    }
    private static void Text(TMP_Text text, Vector2 size, Vector2 position, float font)
    { text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position; text.fontSize = font; }
    private static void Place(RectTransform rect, float w, float h, float x, float y)
    { rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y); }
    private static RectTransform Rect(string name, Transform parent, float w, float h)
    {
        var rect = parent.Cast<Transform>().FirstOrDefault(t => t.name == name) as RectTransform;
        if (rect == null) { rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); }
        rect.gameObject.layer = 5; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = Vector2.zero; return rect;
    }
    private static T Get<T>(Component parent) where T : Component
    { var existing = parent.GetComponent<T>(); return existing != null ? existing : parent.gameObject.AddComponent<T>(); }
}

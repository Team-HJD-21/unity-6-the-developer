using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 무기 카드와 탄약만 하나의 편집 가능한 패널로 옮깁니다. 다른 HUD는 재생성하지 않습니다.</summary>
public static class HudAmmoDockSetup
{
    [MenuItem("Tools/TeamHJD/The Developer/HUD v6/Apply weapon magazine dock")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        const string path = "Assets/Prefabs/UI/PlayerSkillHUD.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Apply(root, AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void Apply(GameObject root, CombatHudStyle style)
    {
        var stack = root.GetComponentInChildren<CombatWeaponStack>(true);
        var ammo = root.GetComponentInChildren<CombatPlayerIndicators>(true);
        var panel = Rect("Weapon Magazine Panel", stack.transform, 450, 100, new(0, 0), new(0, 0), new(50, 64));
        panel.SetAsFirstSibling(); stack.PanelRect = panel; stack.CardPosition = new Vector2(50, 64); stack.NamePosition = new Vector2(175, 35);
        Frame(panel, "Surface", style.Get("weapon_surface"), Color.white).raycastTarget = true;
        stack.PanelEdge = Frame(panel, "Edge", style.Get("weapon_edge_idle"), new Color(.57f, .63f, .59f, .7f));
        var divider = Picture(Rect("Magazine Divider", panel, 1, 62, Vector2.zero, new(.5f, .5f), new(256, 50)),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/HudV6/fill_white_solid.asset"));
        divider.color = new Color(.48f, .53f, .49f, .45f);
        foreach (var slot in stack.Slots)
        {
            var rect = (RectTransform)slot.transform;
            Place(rect, 250, 100, Vector2.zero, Vector2.zero, stack.CardPosition);
            foreach (string name in new[] { "V6 Surface", "V6 Panel Edge", "V6 Edge", "V6 Selection" })
            { var old = rect.Find(name); if (old != null && old.TryGetComponent<Graphic>(out var graphic)) graphic.enabled = false; }
            var icon = rect.Find("V6 Weapon").GetComponent<Image>();
            Place(icon.rectTransform, 234, 72, new(.5f, .5f), new(.5f, .5f), new(3, 0)); icon.enabled = true; icon.preserveAspect = true;
            var hit = Picture(Rect("Weapon Hit Area", rect, 250, 100, new(.5f, .5f), new(.5f, .5f), Vector2.zero), null);
            hit.color = new Color(1, 1, 1, .001f); hit.raycastTarget = true; hit.transform.SetAsFirstSibling();
            var button = slot.GetComponent<Button>(); button.targetGraphic = hit; button.transition = Selectable.Transition.None;
        }
        var numbers = (RectTransform)stack.transform.Find("Weapon Numbers");
        numbers.anchoredPosition = new Vector2(10, 70); numbers.sizeDelta = new Vector2(28, 88); numbers.SetAsLastSibling();
        stack.NumberShadows = new TMP_Text[stack.NumberButtons.Length];
        for (int i = 0; i < stack.NumberButtons.Length; i++)
        {
            ((RectTransform)stack.NumberButtons[i].transform).sizeDelta = new Vector2(28, 22);
            ((RectTransform)stack.NumberButtons[i].transform).anchoredPosition = new Vector2(0, -11 - 22 * i);
            stack.NumberLabels[i].rectTransform.sizeDelta = new Vector2(28, 22);
            stack.NumberButtons[i].targetGraphic.enabled = true; stack.NumberLabels[i].enabled = true;
            var shadow = Get<TextMeshProUGUI>(Rect("Number Shadow", stack.NumberButtons[i].transform, 28, 22, new(.5f, .5f), new(.5f, .5f), new(1, -1)));
            shadow.font = stack.NumberLabels[i].font; shadow.text = (i + 1).ToString(); shadow.fontSize = stack.NumberLabels[i].fontSize;
            shadow.color = new Color(.015f, .02f, .02f, .9f); shadow.alignment = TextAlignmentOptions.Center;
            shadow.textWrappingMode = TextWrappingModes.NoWrap; shadow.raycastTarget = false; shadow.enabled = true;
            shadow.transform.SetAsFirstSibling(); stack.NumberShadows[i] = shadow;
        }
        Place(stack.NameRect, 250, 32, Vector2.zero, new(.5f, .5f), stack.NamePosition);
        stack.NameLabel.rectTransform.sizeDelta = new Vector2(226, 28);
        foreach (var graphic in stack.NameRect.GetComponentsInChildren<Graphic>(true)) graphic.enabled = true;
        ((RectTransform)stack.transform).sizeDelta = new Vector2(520, 210);

        ammo.AmmoRoot.SetParent(panel, false);
        Place(ammo.AmmoRoot, 176, 80, Vector2.zero, new(.5f, .5f), new(350, 50));
        ammo.AmmoGroup.alpha = 1; ammo.AmmoGroup.blocksRaycasts = ammo.AmmoGroup.interactable = false;
        ammo.AmmoContent = Rect("Magazine Content", ammo.AmmoRoot, 176, 80, new(.5f, .5f), new(.5f, .5f), Vector2.zero);
        // Remove the previous authored cartridge strip from either the base or docked layout.
        foreach (var parent in new[] { ammo.AmmoRoot, ammo.AmmoContent })
        for (int i = 0; i < 32; i++)
        {
            foreach (string name in new[] { "Bullet " + i, "Spent Round " + i })
            { var old = parent.Find(name); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject); }
        }
        ammo.RoundCount = Label("Round Count", ammo.AmmoContent, style.LabelFont, style.Text, 26, Vector2.zero);
        ammo.RoundCount.text = "15<size=65%><color=#9AA39B> / 15</color></size>";
        ammo.InfiniteLabel = Label("Infinite", ammo.AmmoContent, style.BodyFont, style.Text, 34, Vector2.zero);
        if (!style.BodyFont.HasCharacter('∞'))
        {
            if (!style.BodyFont.TryAddCharacters("∞", out string missing)) throw new InvalidOperationException("Missing infinite magazine glyph: " + missing);
            EditorUtility.SetDirty(style.BodyFont); AssetDatabase.SaveAssetIfDirty(style.BodyFont);
        }
        ammo.InfiniteLabel.text = "∞"; ammo.InfiniteLabel.gameObject.SetActive(false);
        ammo.ReloadRoot.transform.SetParent(ammo.AmmoContent, false);
        Place((RectTransform)ammo.ReloadRoot.transform, 28, 28, new(.5f, .5f), new(.5f, .5f), new(0, -20));
        Place(ammo.ReloadArrows, 28, 28, new(.5f, .5f), new(.5f, .5f), Vector2.zero);
        var oldArrow = ammo.ReloadArrows.GetComponent<Image>(); if (oldArrow != null) oldArrow.enabled = false;
        var oldVector = ammo.ReloadArrows.Find("Vector");
        if (oldVector != null) UnityEngine.Object.DestroyImmediate(oldVector.gameObject);
        var vector = Rect("Clean Symbol", ammo.ReloadArrows, 28, 28, new(.5f, .5f), new(.5f, .5f), Vector2.zero);
        var symbol = Get<CombatHudSymbol>(vector); symbol.Symbol = CombatHudSymbol.Shape.Reload;
        symbol.enabled = true; symbol.Stroke = 1.6f; symbol.color = style.Accent; symbol.raycastTarget = false;
        ammo.ReloadRoot.SetActive(false);
        // The whole magazine now inherits the weapon's scale, fold animation, input block and first-HUD scan.
        var scale = root.GetComponent<CombatHudScaleBinding>();
        scale.Elements = scale.Elements.Where(e => e.Target != ammo.AmmoRoot).ToArray();
    }
    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, Color color, float size, Vector2 position)
    {
        var text = Get<TextMeshProUGUI>(Rect(name, parent, 166, 36, new(.5f, .5f), new(.5f, .5f), position));
        text.font = font; text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false; text.enabled = true; return text;
    }
    private static Image Frame(Transform parent, string name, Sprite sprite, Color color)
    {
        var image = Picture(Rect(name, parent, 0, 0, new(.5f, .5f), new(.5f, .5f), Vector2.zero), sprite);
        image.type = Image.Type.Sliced; image.color = color;
        image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero; return image;
    }
    private static Image Picture(RectTransform rect, Sprite sprite)
    { var image = Get<Image>(rect); image.sprite = sprite; image.enabled = true; image.raycastTarget = false; return image; }
    private static T Get<T>(Component target) where T : Component
    { var value = target.GetComponent<T>(); return value != null ? value : target.gameObject.AddComponent<T>(); }
    private static RectTransform Rect(string name, Transform parent, float width, float height, Vector2 anchor, Vector2 pivot, Vector2 position)
    {
        var rect = parent.Cast<Transform>().FirstOrDefault(t => t.name == name) as RectTransform;
        if (rect == null) { rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); }
        Place(rect, width, height, anchor, pivot, position); return rect;
    }
    private static void Place(RectTransform rect, float width, float height, Vector2 anchor, Vector2 pivot, Vector2 position)
    {
        rect.gameObject.layer = 5; rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
        rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = position;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
}

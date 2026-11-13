using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>무기 이름과 커서만 제작하며 다른 HUD 조정값은 보존합니다.</summary>
public static class HudPointerReviewSetup
{
    private const string CursorPath = "Assets/Prefabs/UI/Common/CombatCursor.prefab";
    [MenuItem("Tools/TeamHJD/The Developer/HUD v6/Apply weapon names and cursor")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        try
        {
            Apply(root, AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath));
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void Apply(GameObject root, CombatHudStyle style)
    {
        const string names = "스패너기본총냉각스나이퍼";
        if (names.Any(c => !style.BodyFont.HasCharacter(c)) && !style.BodyFont.TryAddCharacters(names, out var missing))
            throw new InvalidOperationException("Weapon name glyphs unavailable: " + missing);
        EditorUtility.SetDirty(style.BodyFont); AssetDatabase.SaveAssetIfDirty(style.BodyFont);
        var stack = root.GetComponentInChildren<CombatWeaponStack>(true);
        var plate = Rect("Weapon Name", stack.transform, 292, 32);
        plate.anchorMin = plate.anchorMax = Vector2.zero;
        plate.anchoredPosition = new Vector2(203, 35);
        var group = Get<CanvasGroup>(plate); group.alpha = 0; group.blocksRaycasts = group.interactable = false;
        Frame(plate, "Surface", style.Get("panel_surface"));
        Frame(plate, "Edge", style.Get("panel_edge_idle"));
        var label = Get<TextMeshProUGUI>(Rect("Name", plate, 266, 28));
        label.font = style.BodyFont; label.fontSize = 21; label.color = style.Text;
        label.text = "기본총"; label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
        stack.SkillHud = root.GetComponent<PlayerSkillHud>(); stack.NameGroup = group;
        stack.NameRect = plate; stack.NameLabel = label;
        var cursor = Get<CombatFieldCursor>(root.transform);
        cursor.Hud = stack.SkillHud; cursor.ViewPrefab = CursorPrefab(style);
        cursor.Weapons = stack; cursor.Header = (RectTransform)root.GetComponentInChildren<CombatHudHeader>(true).transform;
    }
    private static CombatCursorView CursorPrefab(CombatHudStyle style)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CursorPath);
        if (existing != null) return existing.GetComponent<CombatCursorView>();
        var root = new GameObject("Combat Cursor", typeof(RectTransform), typeof(Canvas), typeof(CombatCursorView));
        try
        {
            root.layer = 5;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var view = root.GetComponent<CombatCursorView>(); view.IdleColor = style.Text; view.ShotColor = style.Accent;
            view.Reticle = Rect("Reticle", root.transform, 32, 32);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/Cursors/target_a.png");
            if (texture == null) throw new InvalidOperationException("Missing authored target_a cursor.");
            var shadow = Get<RawImage>(Rect("Shadow", view.Reticle, 32, 32));
            shadow.texture = texture; shadow.color = new Color(0, 0, 0, .85f); shadow.raycastTarget = false;
            shadow.rectTransform.anchoredPosition = new Vector2(1, -1);
            view.Art = Get<RawImage>(Rect("Art", view.Reticle, 32, 32));
            view.Art.texture = texture; view.Art.color = style.Text; view.Art.raycastTarget = false;
            return PrefabUtility.SaveAsPrefabAsset(root, CursorPath).GetComponent<CombatCursorView>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static void Frame(RectTransform parent, string name, Sprite sprite)
    {
        var image = Get<Image>(Rect(name, parent, 0, 0)); image.sprite = sprite; image.type = Image.Type.Sliced;
        image.raycastTarget = false; image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
    }
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

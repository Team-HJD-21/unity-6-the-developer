using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>피드백 리뷰 대상만 갱신하며 기존 HUD 참조와 편집 값을 보존합니다.</summary>
public static class HudFeedbackReviewSetup
{
    [MenuItem("Tools/TeamHJD/The Developer/HUD v6/Apply feedback review")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var style = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath);
        Patch("Assets/Prefabs/UI/PlayerSkillHUD.prefab", root => ApplySkill(root, style));
        Patch("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab", root => ApplyVitals(root, style));
    }
    private static void Patch(string path, Action<GameObject> apply)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void ApplySkill(GameObject root, CombatHudStyle style)
    {
        var picker = root.GetComponentInChildren<CombatSkillPicker>(true);
        var layout = Get<CombatHudAlertLayout>(root.transform); layout.Hud = root.GetComponent<PlayerSkillHud>(); layout.Picker = picker;
        var compactTooltip = (PlayerSkillTooltipView)new SerializedObject(layout.Hud).FindProperty("_tooltip").objectReferenceValue;
        layout.CompactTooltip = compactTooltip != null ? (RectTransform)compactTooltip.transform : null;
        Needle(picker.QNeedle, style, 100); Needle(picker.ENeedle, style, 142);
        picker.QKeyBadge = Badge("Q Key Badge", picker.PickerRect, picker.QNeedleLabel, style);
        picker.EKeyBadge = Badge("E Key Badge", picker.PickerRect, picker.ENeedleLabel, style);
        // All stems precede both opaque key faces, including while the pointers cross.
        picker.QNeedle.SetAsLastSibling(); picker.ENeedle.SetAsLastSibling();
        picker.QKeyBadge.SetAsLastSibling(); picker.EKeyBadge.SetAsLastSibling();
        foreach (var wedge in picker.Wedges)
        {
            wedge.Art = (RectTransform)wedge.transform.Find("Art");
            var shadow = wedge.transform.Find("Lift Shadow");
            if (shadow == null)
            {
                shadow = UnityEngine.Object.Instantiate(wedge.Art, wedge.transform, false);
                shadow.name = "Lift Shadow";
            }
            shadow.SetSiblingIndex(wedge.Art.GetSiblingIndex());
            wedge.Art.SetSiblingIndex(shadow.GetSiblingIndex() + 1);
            foreach (var image in shadow.GetComponentsInChildren<Image>(true))
            { image.color = new Color(.03f, .035f, .04f, 1); image.raycastTarget = false; }
            wedge.LiftShadow = Get<CanvasGroup>(shadow); wedge.LiftShadow.alpha = 0;
            wedge.LiftShadow.blocksRaycasts = wedge.LiftShadow.interactable = false;
            var grip = Rect("Drag Grip", wedge.Art, 15, 20);
            grip.anchoredPosition = new Vector2(51, -16);
            Shape(grip, CombatHudShape.Form.Grip, style.Accent);
            wedge.Grip = Get<CanvasGroup>(grip); wedge.Grip.alpha = 0;
            wedge.Grip.blocksRaycasts = wedge.Grip.interactable = false;
        }
        foreach (var slot in root.GetComponentsInChildren<PlayerSkillSlotView>(true))
        {
            var so = new SerializedObject(slot);
            var fill = (Image)so.FindProperty("_cooldown").objectReferenceValue;
            var pulse = Slot((RectTransform)slot.transform, fill, style);
            so.FindProperty("_inputFeedback").objectReferenceValue = pulse;
            so.FindProperty("_cooldownColor").colorValue = new Color(.045f, .055f, .06f, .68f);
            so.FindProperty("_partialChargeAlpha").floatValue = .3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var dash = root.GetComponentInChildren<PlayerDashHud>(true);
        var ds = new SerializedObject(dash);
        var dashFill = (Image)ds.FindProperty("_cooldownFill").objectReferenceValue;
        ds.FindProperty("_inputFeedback").objectReferenceValue = Slot((RectTransform)dash.transform, dashFill, style);
        dashFill.color = new Color(.045f, .055f, .06f, .68f);
        ds.ApplyModifiedPropertiesWithoutUndo();
        var header = root.GetComponentInChildren<CombatHudHeader>(true);
        var plate = Rect("Header Backplate", header.transform, 520, 64);
        plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(.5f, 1);
        var shape = Shape(plate, CombatHudShape.Form.HeaderPlate, new Color(style.Accent.r, style.Accent.g, style.Accent.b, .3f));
        shape.Face = new Color(.035f, .045f, .05f, .82f); shape.Stroke = 1;
        plate.SetAsFirstSibling();
        var rule = header.transform.Find("Rule"); if (rule != null) rule.gameObject.SetActive(false);
    }
    public static void ApplyVitals(GameObject root, CombatHudStyle style)
    {
        foreach (var group in root.GetComponentsInChildren<CombatVitalsGroup>(true))
        {
            var bar = group.Primary; bar.DamageFeedback = true; bar.HitColor = new Color(.98f, .77f, .59f);
            var fill = bar.Fill;
            var trailRect = Rect("Damage Trail", fill.transform.parent, fill.rectTransform.rect.width, fill.rectTransform.rect.height);
            trailRect.anchorMin = fill.rectTransform.anchorMin; trailRect.anchorMax = fill.rectTransform.anchorMax;
            trailRect.pivot = fill.rectTransform.pivot; trailRect.sizeDelta = fill.rectTransform.sizeDelta;
            trailRect.anchoredPosition = fill.rectTransform.anchoredPosition;
            var trail = Get<Image>(trailRect); trail.sprite = fill.sprite; trail.type = Image.Type.Filled;
            trail.fillMethod = fill.fillMethod; trail.fillOrigin = fill.fillOrigin; trail.fillClockwise = fill.fillClockwise;
            trail.fillAmount = 0; trail.color = new Color(.97f, .72f, .5f, .8f); trail.raycastTarget = false;
            trailRect.SetSiblingIndex(fill.transform.GetSiblingIndex());
            fill.transform.SetSiblingIndex(trailRect.GetSiblingIndex() + 1);
            bar.DamageTrail = trail;
        }
    }
    private static CombatSkillSlotFeedback Slot(RectTransform parent, Image fill, CombatHudStyle style)
    {
        var maskRect = Rect("Whole Slot Cooldown", parent, 132, 137); Stretch(maskRect);
        var oldMask = maskRect.GetComponent<Image>();
        if (oldMask != null) UnityEngine.Object.DestroyImmediate(oldMask);
        Shape(maskRect, CombatHudShape.Form.SlotSilhouette, Color.white);
        Get<Mask>(maskRect).showMaskGraphic = false;
        fill.transform.SetParent(maskRect, false); Stretch(fill.rectTransform);
        fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/HudV6/fill_white_solid.asset");
        if (fill.sprite == null) throw new InvalidOperationException("Missing solid Filled sprite.");
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Vertical; fill.fillOrigin = 0; fill.raycastTarget = false;
        maskRect.SetAsLastSibling();
        var press = Get<Image>(Rect("Key Press", parent, 132, 137)); Stretch(press.rectTransform);
        press.sprite = style.Get("skill_surface"); press.type = Image.Type.Sliced; press.color = Color.clear; press.raycastTarget = false;
        press.transform.SetAsLastSibling();
        var feedback = Get<CombatSkillSlotFeedback>(parent); feedback.Overlay = press;
        return feedback;
    }
    private static void Needle(RectTransform root, CombatHudStyle style, float radius)
    {
        root.anchorMin = root.anchorMax = new Vector2(.5f, 0); root.pivot = new Vector2(.5f, 0);
        root.sizeDelta = new Vector2(30, 200); root.anchoredPosition = Vector2.zero;
        var old = root.GetComponent<Image>(); if (old != null) old.enabled = false;
        var line = Rect("Pointer", root, 30, 200); line.pivot = new Vector2(.5f, 0);
        line.anchorMin = line.anchorMax = new Vector2(.5f, 0);
        var graphic = Shape(line, CombatHudShape.Form.Needle, style.Accent); graphic.KeyRadius = radius; graphic.Stroke = 1.8f;
    }
    private static RectTransform Badge(string name, Transform parent, TMP_Text label, CombatHudStyle style)
    {
        var rect = Rect(name, parent, 36, 36); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
        Shape(rect, CombatHudShape.Form.KeyBadge, style.Accent);
        label.transform.SetParent(rect, false); Stretch(label.rectTransform);
        label.fontSize = 21; label.color = style.Text; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; return rect;
    }
    private static CombatHudShape Shape(RectTransform rect, CombatHudShape.Form form, Color tint)
    {
        Get<CanvasRenderer>(rect);
        var shape = Get<CombatHudShape>(rect); shape.Shape = form; shape.color = tint; shape.raycastTarget = false; return shape;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static RectTransform Rect(string name, Transform parent, float w, float h)
    {
        var rect = parent.Cast<Transform>().FirstOrDefault(t => t.name == name) as RectTransform;
        if (rect == null) { rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); }
        rect.gameObject.layer = 5; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = Vector2.zero; return rect;
    }
    private static T Get<T>(Component target) where T : Component
    { var value = target.GetComponent<T>(); return value != null ? value : target.gameObject.AddComponent<T>(); }
}

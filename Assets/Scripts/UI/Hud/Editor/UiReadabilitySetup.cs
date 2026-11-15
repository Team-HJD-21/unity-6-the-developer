// 기존 공용 메뉴·알림·HUD 프리팹의 글자 크기와 줄바꿈을 갱신하는 명시적 Editor 제작 도구입니다.
// Scene과 원본 폰트는 저장하지 않습니다.
using System;
using System.Linq;
using TeamHJD.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>확인된 텍스트 영역만 보정합니다. 원본 폰트와 Scene, 다른 제작물은 저장하지 않습니다.</summary>
public static class UiReadabilitySetup
{
    [MenuItem("Tools/TeamHJD/The Developer/Common UI/Apply text readability")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var style = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath);
        foreach (string name in new[] { "MenuScreen", "ResultScreen", "RecoveryScreen" })
            Patch("Assets/Prefabs/UI/Common/" + name + ".prefab", ApplyPanel);
        Patch("Assets/Prefabs/UI/PlayerSkillHUD.prefab", root => ApplyHud(root, style));
        Patch("Assets/Prefabs/UI/Common/LoadingScreen.prefab", UiProductPolishSetup.ApplyLoading);
        Patch(AlertPrefabSetup.PrefabPath, root => AlertPrefabSetup.ApplyReadability(root, style));
        Patch(AlertPrefabSetup.CanvasPath, root =>
        {
            var rect = (RectTransform)root.GetComponent<AppScreenCanvasView>().Alerts.transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 76);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        });
        TMP_Settings.useModernHangulLineBreakingRules = true;
        EditorUtility.SetDirty(TMP_Settings.instance); AssetDatabase.SaveAssetIfDirty(TMP_Settings.instance);
        Debug.Log("UI_READABILITY_AUTHORED");
    }
    private static void ApplyPanel(GameObject root)
    {
        var view = root.GetComponent<AppPanelView>();
        if (root.name == "MenuScreen") view.Panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 508);
        if (root.name == "RecoveryScreen") view.Panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 520);
        view.MinimumScreenScale = .8f; view.ScreenPadding = 24;
        if (view.Status == null) return;
        view.Status.fontSize = 20;
        view.Status.textWrappingMode = TextWrappingModes.Normal;
        view.Status.overflowMode = TextOverflowModes.Overflow;
        view.Status.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, root.name == "MenuScreen" ? 52 : 48);
    }
    public static void ApplyHud(GameObject root, CombatHudStyle style)
    {
        var picker = root.GetComponentInChildren<CombatSkillPicker>(true);
        if (picker != null)
        {
            picker.TooltipRect.sizeDelta = new Vector2(368, 148);
            picker.TooltipTitle.rectTransform.sizeDelta = new Vector2(332, 32);
            picker.TooltipTitle.rectTransform.anchoredPosition = new Vector2(0, 48);
            picker.TooltipBody.rectTransform.sizeDelta = new Vector2(332, 88);
            picker.TooltipBody.rectTransform.anchoredPosition = new Vector2(0, -18);
            picker.TooltipBody.textWrappingMode = TextWrappingModes.Normal;
        }
        foreach (string key in new[] { "Q", "E" })
        {
            var label = root.transform.Find("V6 Compact Dock/V6 Compact/" + key + " Skill Slot/Key")?.GetComponent<TMP_Text>();
            if (label == null) continue;
            label.font = style.LabelFont; label.fontSize = 22;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(30, 30);
        }
        var dash = root.transform.Find("V6 Compact Dock/V6 Compact/Dash Slot/Status")?.GetComponent<TMP_Text>();
        if (dash != null) dash.textWrappingMode = TextWrappingModes.NoWrap;
        var ammo = root.GetComponentInChildren<CombatPlayerIndicators>(true);
        if (ammo != null && ammo.InfiniteLabel != null)
            ammo.InfiniteLabel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 44);
        var stack = root.GetComponentInChildren<CombatWeaponStack>(true);
        if (stack != null)
            foreach (var labels in new[] { stack.NumberLabels, stack.NumberShadows })
                if (labels != null) foreach (var label in labels)
                    if (label != null) { label.rectTransform.sizeDelta = new Vector2(36, 30); label.textWrappingMode = TextWrappingModes.NoWrap; }
        var scale = root.GetComponent<CombatHudScaleBinding>();
        if (scale != null)
        {
            var elements = (scale.Elements ?? Array.Empty<CombatHudScaleBinding.Element>()).ToList();
            Floor(picker != null ? picker.TooltipRect : null, .8f);
            Floor(root.GetComponentInChildren<PlayerSkillTooltipView>(true)?.transform as RectTransform, .75f);
            Floor(stack != null ? stack.NameRect : null, .75f);
            scale.Elements = elements.ToArray();
            void Floor(RectTransform target, float minimum)
            {
                if (target == null) return;
                int index = elements.FindIndex(e => e.Target == target);
                var element = index >= 0 ? elements[index] : new CombatHudScaleBinding.Element { Target = target, MinimumScale = 1 };
                element.MinimumScreenScale = minimum;
                if (index >= 0) elements[index] = element; else elements.Add(element);
            }
        }
        var slots = root.transform.Find("Turret Slots");
        if (slots != null) foreach (var label in slots.GetComponentsInChildren<TMP_Text>(true))
            if (label.name == "Number") label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 176);
    }
    private static void Patch(string path, Action<GameObject> apply)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

// 기존 기능 오브젝트와 참조를 유지한 채 공통 프레임/버튼을 제작합니다. Runtime 테마 치환은 하지 않습니다.
using System;
using System.Linq;
using TeamHJD.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CommonFeatureUiSetup
{
    private static CombatHudStyle _style;
    private static readonly Color Face = new(.11f, .14f, .15f, 1);
    private static readonly Color Recess = new(.085f, .11f, .12f, 1);
    private static readonly Color Muted = new(.66f, .69f, .65f, 1);

    [MenuItem("Tools/TeamHJD/The Developer/Common UI/Style existing feature panels")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        _style = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath);
        const string path = "Assets/Prefabs/UI/InitialStagePlayerHUD.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 상태/포탑 패널은 Field 컨텍스트 프롬프트와 스킬 HUD보다 위에 표시합니다.
            root.GetComponent<Canvas>().sortingOrder = 30;
            foreach (string name in new[] { "Player Status Panel", "TurretControlPanel" })
            {
                var panel = root.transform.Find(name);
                if (panel == null) throw new InvalidOperationException("Missing authored panel: " + name);
                StylePanel(panel);
            }
            var status = root.transform.Find("Player Status Panel");
            // 미구현 재화 자리와 화면 자체를 설명하는 반복 안내는 표시하지 않습니다.
            foreach (var text in status.GetComponentsInChildren<TMP_Text>(true))
            {
                if (new[] { "Close Hint", "Weapon Detail Hint", "Rewards Instruction", "Skill Tree Scroll Hint" }.Contains(text.name)
                    || text.name.StartsWith("Currency ", StringComparison.Ordinal)) text.gameObject.SetActive(false);
                if (text.name == "Debug Feedback") text.text = string.Empty;
                if (text.name == "Title" || text.text == "PLAYER STATUS")
                { text.font = _style.HeaderFont; text.fontSize = 26; }
            }
            var divider = status.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Currency Divider");
            if (divider != null) divider.gameObject.SetActive(false);
            HideUnassignedSkillNodes(root.GetComponentInChildren<PlayerSkillTreePanel>(true));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    private static void HideUnassignedSkillNodes(PlayerSkillTreePanel tree)
    {
        var cards = new SerializedObject(tree).FindProperty("_cards");
        var scroll = tree.GetComponentsInChildren<ScrollRect>(true).Single(s => s.name == "Skill Circuit Scroll");
        float bottom = 0; int last = 0;
        for (int i = 0; i < cards.arraySize; i++)
        {
            var card = cards.GetArrayElementAtIndex(i);
            bool assigned = card.FindPropertyRelative("Skill").intValue != (int)PlayerSkillId.None;
            var title = (TMP_Text)card.FindPropertyRelative("Title").objectReferenceValue;
            var summary = (TMP_Text)card.FindPropertyRelative("Summary").objectReferenceValue;
            var node = title.transform.parent.parent as RectTransform;
            if (node == null || !node.name.StartsWith("Skill Circuit Node ", StringComparison.Ordinal)) throw new InvalidOperationException("Preserve the authored skill-card hierarchy.");
            node.gameObject.SetActive(assigned); summary.gameObject.SetActive(assigned);
            if (assigned) { last = i + 1; bottom = Mathf.Max(bottom, -node.anchoredPosition.y + node.rect.height * .5f + 80); }
        }
        foreach (Transform item in scroll.content)
            if (item.name.StartsWith("Circuit ", StringComparison.Ordinal) && int.TryParse(item.name.Split(' ')[1], out int circuit))
                item.gameObject.SetActive(circuit < last);
        scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(550, bottom));
    }

    private static void StylePanel(Transform root)
    {
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.name.StartsWith("Preview ", StringComparison.Ordinal) || image.name == "Weapon Icon") continue;
            var color = image.color;
            if (image.sprite != null || color.a <= .001f) continue;
            if (image.name == "Health Fill") image.color = _style.Health;
            else if (image.name.EndsWith("Stroke", StringComparison.Ordinal)) image.color = _style.Text;
            else if (image.name == "Experience Fill" || image.name.Contains("Handle") || image.name.Contains("Junction")) image.color = _style.Accent;
            else if (image.name.Contains("Trace") || image.name.Contains("Divider")) image.color = new Color(.42f, .45f, .40f, color.a);
            else image.color = image.name.Contains("Track") || image.name.Contains("Viewport") || image.name.Contains("Row ") || image.name.Contains("Region") ? Recess : Face;
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            var c = text.color;
            text.font = _style.BodyFont;
            bool semantic = c == Color.green || c == Color.red || c == Color.cyan;
            text.color = semantic ? c : c == _style.Accent ? _style.Accent : c.r < .7f ? Muted : _style.Text;
        }
        Frame(root, false);
        var background = root.Find("Panel Background");
        if (background != null) Frame(background, true);
        var debug = root.Find("Player Progression Debug Panel");
        if (debug != null) Frame(debug, false);
        foreach (var outline in root.GetComponentsInChildren<Outline>(true)) outline.effectColor = new Color(.45f, .49f, .44f, .6f);
        foreach (var button in root.GetComponentsInChildren<Button>(true)) StyleButton(button);
    }

    private static void Frame(Transform parent, bool opaque)
    {
        var original = parent.GetComponent<Image>();
        if (original != null)
        {
            original.sprite = null; original.type = Image.Type.Simple; original.color = Face;
        }
        if (!opaque)
        {
            var edge = ChildImage(parent, "Common Panel Edge"); edge.sprite = _style.Get("panel_edge_idle");
            edge.type = Image.Type.Sliced; edge.color = Color.white; edge.transform.SetAsLastSibling();
        }
    }

    private static void StyleButton(Button button)
    {
        var target = button.targetGraphic as Image;
        if (target != null)
        { target.sprite = _style.Get("button_surface"); target.type = Image.Type.Sliced; target.color = Color.white; }
        var edge = ChildImage(button.transform, "Common Button Edge");
        edge.sprite = _style.Get("button_edge_idle"); edge.type = Image.Type.Sliced; edge.color = Color.white;
        var feedback = button.GetComponent<UiButtonFeedback>();
        if (feedback == null) feedback = button.gameObject.AddComponent<UiButtonFeedback>();
        feedback.Label = button.GetComponentInChildren<TMP_Text>(true); feedback.Edge = edge;
        feedback.PreserveLabelColor = button.name == "ActivationButton" && button.GetComponentInParent<StageTurretControlView>(true) != null;
        feedback.IdleEdge = edge.sprite; feedback.HoverEdge = _style.Get("button_edge_hover");
        feedback.Text = _style.Text; feedback.Accent = _style.Accent; feedback.HoverSeconds = _style.HoverSeconds;
        button.transition = Selectable.Transition.ColorTint;
        var colors = ColorBlock.defaultColorBlock; colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white; colors.pressedColor = new Color(.72f, .72f, .67f);
        colors.disabledColor = new Color(.5f, .5f, .47f, .5f); colors.fadeDuration = .08f; button.colors = colors;
    }

    private static Image ChildImage(Transform parent, string name)
    {
        var child = parent.Find(name) as RectTransform;
        if (child == null) { child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); child.SetParent(parent, false); }
        child.anchorMin = Vector2.zero; child.anchorMax = Vector2.one; child.offsetMin = child.offsetMax = Vector2.zero;
        var image = child.GetComponent<Image>(); if (image == null) image = child.gameObject.AddComponent<Image>();
        image.raycastTarget = false; return image;
    }
}

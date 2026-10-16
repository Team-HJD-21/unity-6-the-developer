// 기존 B HUD 프리팹을 보존하면서 CU 자동 활성화·지역 상태 View를 연결합니다.
// 수동/배치 메뉴에서만 에셋을 수정하며 Runtime에 UI 계층을 임의 생성하지 않습니다.
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class StageControlUnitPrefabSetup
{
    private const string Path = "Assets/Prefabs/UI/InitialStagePlayerHUD.prefab";
    [MenuItem("Tools/TeamHJD/The Developer/Configure CU HUD")]
    public static void Configure()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var hud = root.GetComponent<InitialStagePlayerHUD>();
            if (hud == null) throw new InvalidOperationException("HUD prefab root is missing.");
            var existing = root.GetComponentsInChildren<StageControlUnitView>(true);
            if (existing.Length > 0)
            {
                if (existing.Length != 1 || !existing[0].IsConfigured) throw new InvalidOperationException("Review the existing CU controls before replacing them.");
                Set(hud, "controlUnitView", existing[0]);
            }
            else
            {
                TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true).First(text => text.font != null).font;
                RectTransform panel = Rect("Control Unit Controls", root.transform, new Vector2(360, 158));
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0);
                panel.anchoredPosition = new Vector2(-24, 24);
                panel.gameObject.AddComponent<Image>().color = new Color(.035f, .07f, .1f, .94f);
                var view = panel.gameObject.AddComponent<StageControlUnitView>();
                TMP_Text region = Text("Region", panel, font, 12, 12, 336, 46, 15);
                TMP_Text state = Text("Auto Status", panel, font, 12, 58, 336, 20, 14);
                RectTransform buttonRect = Rect("Auto Activate", panel, new Vector2(336, 30));
                buttonRect.anchoredPosition = new Vector2(12, -84);
                buttonRect.gameObject.AddComponent<Image>().color = new Color(.1f, .32f, .4f);
                var button = buttonRect.gameObject.AddComponent<Button>();
                TMP_Text label = Text("Label", buttonRect, font, 0, 0, 336, 30, 15);
                label.alignment = TextAlignmentOptions.Center; label.text = "AUTO ACTIVATE TURRETS";
                TMP_Text feedback = Text("Feedback", panel, font, 12, 120, 336, 26, 12);
                region.text = "Region: waiting for Match"; state.text = "Approach the CU"; feedback.text = "";
                Set(view, "regionText", region); Set(view, "autoStatusText", state);
                Set(view, "feedbackText", feedback); Set(view, "autoActivationButton", button);
                Set(hud, "controlUnitView", view);
            }
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            Debug.Log("CU_HUD_CONFIGURED");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        return rect;
    }
    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float x, float y, float width, float height, float size)
    {
        RectTransform rect = Rect(name, parent, new Vector2(width, height));
        rect.anchoredPosition = new Vector2(x, -y);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = new Color(.82f, .93f, .96f);
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
    private static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(owner);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

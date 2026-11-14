// Alert 공통 프리팹과 App Canvas의 전용 표시 참조만 제작합니다.
// 다른 HUD/Scene과 원본 폰트는 저장하지 않으며 실행은 HUD 통합 작업자가 담당합니다.
using System;
using System.Linq;
using TeamHJD.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class AlertPrefabSetup
{
    public const string PrefabPath = "Assets/Prefabs/UI/Common/AlertStrip.prefab";
    public const string CanvasPath = "Assets/Prefabs/UI/Common/AppScreenCanvas.prefab";
    public const string MaterialPath = "Assets/Prefabs/UI/Common/AlertText.mat";

    [MenuItem("Tools/TeamHJD/The Developer/Common UI/Configure alerts")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var style = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath);
        if (style == null || style.BodyFont == null) throw new InvalidOperationException("Configure the HUD body font first.");
        PrepareGlyphs(style);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath) == null) throw new InvalidOperationException("Configure the common AppScreenCanvas first.");
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("AlertStrip", typeof(RectTransform));
        try
        {
            Build(root, style);
            if (!root.GetComponent<AlertStripView>().IsConfigured)
                throw new InvalidOperationException("Alert component references must be valid before saving the prefab.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }

        var canvas = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            var view = canvas.GetComponent<AppScreenCanvasView>();
            if (view == null) throw new InvalidOperationException("App Canvas view is missing.");
            var alert = view.Alerts;
            if (alert == null)
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), canvas.transform);
                child.name = "AlertStrip"; alert = child.GetComponent<AlertStripView>();
            }
            // Screens 안에 두면 필드 커서가 전체 화면 UI로 오인하므로 전용 형제로 둡니다.
            alert.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)alert.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, alert.DefaultBottom);
            rect.sizeDelta = ((RectTransform)AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform).sizeDelta;
            rect.SetAsLastSibling(); view.TravelFade.transform.SetAsLastSibling();
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            view.Alerts = alert;
            PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(canvas); }
        Debug.Log("COMMON_ALERT_CONFIGURED");
    }

    private static void Build(GameObject root, CombatHudStyle style)
    {
        root.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(720, 64);
        var view = Get<AlertStripView>(root); view.Group = Get<CanvasGroup>(root);
        view.MinimumScreenScale = .8f; view.MinimumScreenWidth = 800;
        view.ErrorColor = new Color(.98f, .72f, .66f);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
        rect.anchoredPosition = new Vector2(0, view.DefaultBottom);
        var background = root.GetComponent<Image>();
        if (background != null) Object.DestroyImmediate(background);
        var renderer = root.GetComponent<CanvasRenderer>();
        if (renderer != null) Object.DestroyImmediate(renderer);
        foreach (string name in new[] { "Frame", "Meaning Color" })
        { var old = rect.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject); }
        var textRect = Child(rect, "Message"); Stretch(textRect, 0, 0, 0, 0);
        view.Message = Get<TextMeshProUGUI>(textRect.gameObject);
        view.Message.font = style.BodyFont; view.Message.color = style.Text;
        view.Message.fontSharedMaterial = PrepareMaterial(style.BodyFont);
        view.Message.fontSize = 26; view.Message.enableAutoSizing = true;
        view.Message.fontSizeMin = 22; view.Message.fontSizeMax = 26;
        view.Message.maxVisibleLines = 2;
        view.Message.textWrappingMode = TextWrappingModes.Normal;
        view.Message.overflowMode = TextOverflowModes.Ellipsis;
        view.Message.alignment = TextAlignmentOptions.Center;
        view.Message.richText = false; view.Message.raycastTarget = false;
        view.Hide();
    }

    private static Material PrepareMaterial(TMP_FontAsset font)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(font.material) { name = "AlertText" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        { material.shader = font.material.shader; material.CopyPropertiesFromMaterial(font.material); }
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(.015f, .02f, .02f, 1));
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, .14f);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, .08f);
        material.EnableKeyword("OUTLINE_ON");
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static void PrepareGlyphs(CombatHudStyle style)
    {
        const string fontPath = "Assets/Art/UI/HudV6/CombatHudBodySDF.asset";
        if (AssetDatabase.GetAssetPath(style.BodyFont) != fontPath)
            throw new InvalidOperationException("Alert glyphs must use the dedicated CombatHudBodySDF, not an original font asset.");
        const string text = "Q E 슬롯에 스킬이 없습니다. 장착하세요. " +
            "적용했습니다. 터렛을 활성화했습니다. 비활성화했습니다. 레벨을 올렸습니다. 낮췄습니다. 강화했습니다. " +
            "자동 활성화를 시작했습니다. 사용할 수 있는 전력이 부족합니다. 대상에 더 가까이 이동하세요. " +
            "쓰러진 상태에서는 조작할 수 없습니다. 지금은 조작할 수 없습니다. 조작할 대상을 찾을 수 없습니다. " +
            "파괴된 터렛입니다. 아직 해금되지 않은 터렛입니다. 이미 적용된 상태입니다. 요청을 보낼 수 없거나 처리 중입니다. " +
            "구간 시간과 날씨를 초기화했습니다. 낮밤 전환을 요청했습니다. 현재는 요청할 수 없습니다. CU와의 거리와 플레이어 상태를 확인하세요. " +
            "몬스터와 겹쳐 시한폭탄을 설치할 수 없습니다. 장애물과 겹쳐 설치할 수 없습니다. 시한폭탄 본체 설정을 확인해 주세요. " +
            "지면이 부족합니다. 안쪽으로 이동해 주세요. 다른 포탑과 플레이어와 겹쳐 설치할 수 없습니다. 이곳에는 스킬을 설치할 수 없습니다. " +
            "방벽을 더 설치할 수 없습니다. 방벽 본체 설정을 확인해 주세요. 이 위치에 설치할 수 없습니다. " +
            "유효하지 않은 설치 위치입니다. 설치 가능 범위를 벗어났습니다. 원 안의 위치를 선택해 주세요.";
        // 이미 있는 문자를 재추가하면 TMP가 실패로 돌려줄 수 있어 실제 누락 문자만 요청합니다.
        string required = new(text.Where(character => !char.IsWhiteSpace(character) && !style.BodyFont.HasCharacter(character)).Distinct().ToArray());
        if (required.Length == 0) return;
        style.BodyFont.TryAddCharacters(required, out _);
        string missing = new(required.Where(character => !style.BodyFont.HasCharacter(character)).ToArray());
        if (missing.Length > 0) throw new InvalidOperationException("Alert body glyphs are missing: " + missing);
        // 전체 SaveAssets는 사용자/원본 폰트를 함께 저장할 수 있으므로 전용 Asset만 저장합니다.
        EditorUtility.SetDirty(style.BodyFont);
        AssetDatabase.SaveAssetIfDirty(style.BodyFont);
    }

    private static T Get<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
    private static RectTransform Child(RectTransform parent, string name)
    {
        var child = parent.Find(name) as RectTransform;
        if (child != null) { child.gameObject.layer = parent.gameObject.layer; return child; }
        child = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        child.gameObject.layer = parent.gameObject.layer;
        child.SetParent(parent, false); return child;
    }
    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
}

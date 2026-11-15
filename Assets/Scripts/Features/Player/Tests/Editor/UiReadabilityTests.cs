// 실제 UI 프리팹의 줄바꿈·가독성 하한·viewport 경계와 버튼 간격을 검사합니다.
// 원본 프리팹과 폰트 에셋은 저장하지 않습니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class UiReadabilityTests
{
    [Test]
    public void SkillSummariesAndLongestCompactDescriptionsFitTheirAuthoredFrames()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        try
        {
            var picker = root.GetComponentInChildren<CombatSkillPicker>(true);
            foreach (PlayerSkillId skill in Enum.GetValues(typeof(PlayerSkillId)))
            {
                if (skill == PlayerSkillId.None) continue;
                Fits(picker.TooltipBody, PlayerSkillDescriptions.Summary(skill));
                if (skill == PlayerSkillId.EnergyBarrier)
                    Assert.That(picker.TooltipBody.GetPreferredValues(PlayerSkillDescriptions.Summary(skill), picker.TooltipBody.rectTransform.rect.width, float.PositiveInfinity).y,
                        Is.LessThanOrEqualTo(picker.TooltipBody.GetPreferredValues("한 줄\n두 줄").y + 1), "핵심 효과가 동사 한 줄을 남기지 않고 두 줄 안에 읽혀야 합니다.");
            }
            var compact = root.GetComponentInChildren<PlayerSkillTooltipView>(true);
            var description = (TMP_Text)new SerializedObject(compact).FindProperty("_description").objectReferenceValue;
            foreach (var skill in new[] { PlayerSkillId.PlayerTurret, PlayerSkillId.EnergyBarrier, PlayerSkillId.LureBeacon })
                Fits(description, "Q · " + PlayerSkillDescriptions.Description(skill, null));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void TwoLineRecoveryFeedbackFitsWithoutTouchingTheMenuButtons()
    {
        var style = Resources.Load<AppScreenStyle>("Core/AppScreenStyle");
        foreach (var asset in new[] { style.MenuPrefab, style.ResultPrefab, style.RecoveryPrefab })
        {
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(asset));
            try
            {
                var panel = root.GetComponent<AppPanelView>();
                Fits(panel.Status, "복귀하지 못했습니다.\n재시도 시 안전장치 추가 차감 없음");
                var status = new Rect(panel.Status.rectTransform.anchoredPosition - Vector2.Scale(panel.Status.rectTransform.sizeDelta, panel.Status.rectTransform.pivot), panel.Status.rectTransform.sizeDelta);
                foreach (var button in panel.Panel.GetComponentsInChildren<Button>(true))
                {
                    var rect = (RectTransform)button.transform;
                    Assert.That(status.Overlaps(new Rect(rect.anchoredPosition - Vector2.Scale(rect.rect.size, rect.pivot), rect.rect.size)), Is.False, asset.name + "/" + button.name);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    [TestCase(800, 600)] [TestCase(1280, 720)] [TestCase(2560, 1080)]
    public void CommonPanelsKeepReadableButtonsAndFitInsideTheActualViewport(int width, int height)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(width, height, 24);
        try
        {
            var cameraObject = new GameObject("Readability test camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.targetTexture = target; camera.scene = scene;
            var style = Resources.Load<AppScreenStyle>("Core/AppScreenStyle");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(style.CanvasPrefab.gameObject, scene);
            root.GetComponent<CanvasScaler>().enabled = false;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
            foreach (var asset in new AppPanelView[] { style.MenuPrefab, style.SettingsPrefab, style.ResultPrefab, style.RecoveryPrefab })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset.gameObject, scene);
                go.transform.SetParent(root.GetComponent<AppScreenCanvasView>().Screens, false);
                var view = go.GetComponent<AppPanelView>();
                Canvas.ForceUpdateCanvases(); view.RefreshReadableScale(); Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4]; view.Panel.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var p = camera.WorldToScreenPoint(corner);
                    Assert.That(p.x, Is.InRange(view.ScreenPadding - 1, width - view.ScreenPadding + 1), asset.name);
                    Assert.That(p.y, Is.InRange(view.ScreenPadding - 1, height - view.ScreenPadding + 1), asset.name);
                }
                Assert.That(view.Primary.GetComponentInChildren<TMP_Text>().fontSize * view.Panel.localScale.x * canvas.scaleFactor, Is.GreaterThanOrEqualTo(16), asset.name);
                Object.DestroyImmediate(go);
            }
            var loading = (GameObject)PrefabUtility.InstantiatePrefab(style.LoadingPrefab.gameObject, scene);
            loading.transform.SetParent(root.GetComponent<AppScreenCanvasView>().Screens, false);
            Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "Brand", "Loading", "Controls", "Progress Track", "Actual Progress" })
            {
                var rect = (RectTransform)loading.transform.Find("Content/" + name);
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var p = camera.WorldToScreenPoint(corner);
                    Assert.That(p.x, Is.InRange(24, width - 24), "Loading/" + name);
                    Assert.That(p.y, Is.InRange(24, height - 24), "Loading/" + name);
                }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); target.Release(); Object.DestroyImmediate(target); }
    }

    [Test]
    public void TooltipScreenFloorIncludesItsScaledParentWithoutEnlargingTheCombatDock()
    {
        var root = new GameObject("Readable HUD test", typeof(RectTransform), typeof(Canvas));
        root.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.scaleFactor = .4f;
            var parent = new GameObject("Combat dock", typeof(RectTransform)); parent.transform.SetParent(root.transform, false); parent.transform.localScale = Vector3.one * .85f;
            var child = new GameObject("Hover name", typeof(RectTransform)); child.transform.SetParent(parent.transform, false);
            var binding = root.AddComponent<CombatHudScaleBinding>();
            binding.Elements = new[] { new CombatHudScaleBinding.Element { Target = (RectTransform)child.transform, MinimumScreenScale = .75f } };
            binding.BindUiDisplaySettings(new UiDisplaySettings(false));
            float expected = .75f * Mathf.Min(1, Screen.width / 800f);
            Assert.That(canvas.scaleFactor * parent.transform.localScale.x * child.transform.localScale.x, Is.EqualTo(expected).Within(.001f));
            Assert.That(parent.transform.localScale.x, Is.EqualTo(.85f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void Fits(TMP_Text label, string value)
    {
        var rect = label.rectTransform.rect;
        var size = label.GetPreferredValues(value, rect.width, float.PositiveInfinity);
        Assert.That(size.x, Is.LessThanOrEqualTo(rect.width + 1), label.name + ": " + value);
        Assert.That(size.y, Is.LessThanOrEqualTo(rect.height + 1), label.name + ": " + value);
    }
}

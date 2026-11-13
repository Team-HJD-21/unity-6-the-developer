// 제작된 Alert의 입력 비점유·의미색·표시 수명과 App Canvas 연결을 검증합니다.
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CommonAlertPresentationTests
{
    [Test]
    public void AuthoredAlertIsOutsideScreenStackAndCannotInterceptPointer()
    {
        var style = Resources.Load<AppScreenStyle>("Core/AppScreenStyle");
        var canvas = Object.Instantiate(style.CanvasPrefab);
        try
        {
            var view = canvas.Alerts;
            Assert.That(view, Is.Not.Null); Assert.That(view.IsConfigured, Is.True);
            Assert.That(view.transform.parent, Is.SameAs(canvas.transform));
            Assert.That(view.transform.IsChildOf(canvas.Screens), Is.False);
            Assert.That(view.transform.GetSiblingIndex(), Is.LessThan(canvas.TravelFade.transform.GetSiblingIndex()));
            Assert.That(view.GetComponentsInChildren<Graphic>(true), Has.All.Matches<Graphic>(graphic => !graphic.raycastTarget));
            Assert.That(view.GetComponentsInChildren<Image>(true), Is.Empty, "Alert는 패널/테두리/의미색 선 없이 텍스트만 표시합니다.");
            using var alerts = new AlertService();
            alerts.Show("사용할 수 있는 전력이 부족합니다.");
            alerts.Tick(1, view.DisplaySeconds); alerts.Tick(1.25, view.DisplaySeconds); view.Render(alerts);
            Assert.That(view.Message.text, Does.Contain("전력"));
            Assert.That(view.Message.maxVisibleLines, Is.EqualTo(2), "TMP의 비직렬화 속성은 실행 인스턴스에서 검증합니다.");
            Assert.That(view.Group.alpha, Is.GreaterThan(.9f));
            Assert.That(view.Message.color, Is.EqualTo(view.ErrorColor));
            Assert.That(view.Group.blocksRaycasts || view.Group.interactable, Is.False);
            alerts.Tick(4, view.DisplaySeconds); view.Render(alerts);
            Assert.That(view.Group.alpha, Is.Zero); Assert.That(view.Message.text, Is.Empty);
            alerts.Show("터렛을 활성화했습니다.", AlertTone.Success);
            alerts.Tick(5, view.DisplaySeconds); view.Render(alerts);
            Assert.That(view.Message.color, Is.EqualTo(view.SuccessColor));
        }
        finally { Object.DestroyImmediate(canvas.gameObject); }
    }

    [Test]
    public void CommonAlertUsesTheDedicatedBodyFontInsteadOfSavingOriginalFonts()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlertPrefabSetup.PrefabPath);
        Assert.That(prefab, Is.Not.Null, "Edit Mode에서 AlertPrefabSetup.Configure를 먼저 실행하세요.");
        var view = prefab.GetComponent<AlertStripView>();
        Assert.That(AssetDatabase.GetAssetPath(view.Message.font), Is.EqualTo("Assets/Art/UI/HudV6/CombatHudBodySDF.asset"));
        Assert.That(view.Message.richText, Is.False);
        Assert.That(AssetDatabase.GetAssetPath(view.Message.fontSharedMaterial), Is.EqualTo(AlertPrefabSetup.MaterialPath));
    }

    [Test]
    public void ScreenClearanceMovesAboveTheHudAndResetReturnsToThePrefabDefault()
    {
        var canvas = Object.Instantiate(Resources.Load<AppScreenStyle>("Core/AppScreenStyle").CanvasPrefab);
        try
        {
            Canvas.ForceUpdateCanvases();
            var view = canvas.Alerts;
            var parent = (RectTransform)canvas.transform;
            var rect = (RectTransform)view.transform;
            float top = RectTransformUtility.WorldToScreenPoint(null, parent.TransformPoint(parent.rect.center)).y;
            view.SetScreenBottomClearance(top);
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Assert.That(RectTransformUtility.WorldToScreenPoint(null, corners[0]).y, Is.GreaterThan(top));
            float raised = rect.anchoredPosition.y;
            view.SetScreenBottomClearance(float.NaN);
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(raised));
            view.SetScreenBottomClearance(float.MaxValue);
            Assert.That(rect.anchoredPosition.y + rect.rect.height, Is.LessThanOrEqualTo(parent.rect.height));
            view.ResetBottomClearance();
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(view.DefaultBottom).Within(.01f));
        }
        finally { Object.DestroyImmediate(canvas.gameObject); }
    }
}

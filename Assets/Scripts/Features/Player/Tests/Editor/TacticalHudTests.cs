using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class TacticalHudTests
{
    [Test]
    public void AuthoredCornersRejectInvalidMarkersAndPreserveTheRectangularStage()
    {
        Assert.That(TacticalMapMath.TryBounds(new Vector2(-138,120),new Vector2(138,-120),out var bounds),Is.True);
        Assert.That(bounds,Is.EqualTo(new Rect(-138,-120,276,240)));
        Assert.That(TacticalMapMath.TryBounds(Vector2.zero,Vector2.zero,out _),Is.False);
        Assert.That(TacticalMapMath.TryBounds(new Vector2(10,-10),new Vector2(-10,10),out _),Is.False);
        Assert.That(TacticalMapMath.TryBounds(new Vector2(float.NaN,10),Vector2.zero,out _),Is.False);
    }
    [Test]
    public void SceneViewportCropsRegionTextureWithoutStretchingCapturedWorldCoordinates()
    {
        var source=new Rect(-137.5f,-137.5f,275,275);var world=new Rect(-138,-120,276,240);
        var area=new Rect(-138,-120,276,240);
        Assert.That(TacticalMapMath.TryRasterPlacement(source,world,area,out var placed,out var uv),Is.True);
        Assert.That(placed,Is.EqualTo(new Rect(-137.5f,-120,275,240)));
        Assert.That(uv.yMin,Is.EqualTo(17.5f/275).Within(.00001));
        Assert.That(uv.height,Is.EqualTo(240f/275).Within(.00001));
        Vector2 territory=new(80,65);
        Vector2 textureUv=(territory-source.min)/source.size;
        Vector2 display=placed.min+Vector2.Scale((textureUv-uv.min)/uv.size,placed.size);
        Assert.That(Vector2.Distance(display,territory),Is.LessThan(.001));
        Assert.That(TacticalMapMath.TryRasterPlacement(new Rect(1000,1000,10,10),world,area,out _,out _),Is.False);
    }
    [Test]
    public void NavigatorPreservesWorldAspectAndClickRoundTripsAtDifferentCanvasSizes()
    {
        var world=new Rect(-80,-40,160,80);
        foreach (float scale in new[] { .5f, 1f, 2f })
        {
            var area=new Rect(-135*scale,-82*scale,270*scale,164*scale);
            var fit=TacticalMapMath.Fit(area,world);
            Assert.That(fit.width/fit.height,Is.EqualTo(2).Within(.0001));
            var p=new Vector2(42,-23);
            var actual=TacticalMapMath.Unproject(TacticalMapMath.Project(p,world,area),world,area);
            Assert.That(Vector2.Distance(p,actual),Is.LessThan(.0001));
            Assert.That(TacticalMapMath.Unproject(new Vector2(-10000,10000),world,area),Is.EqualTo(new Vector2(-80,40)));
        }
    }
    [Test]
    public void EntryFitKeepsPlayerCenteredAndIncludesDistantOccupiedCorners()
    {
        var player=new Vector2(30,-10); var occupied=new Rect(-100,-80,180,160);
        float size=TacticalMapMath.FitAround(player,occupied,16f/9,20);
        var view=TacticalMapMath.CameraRect(player,size,16f/9);
        Assert.That(view.center,Is.EqualTo(player));
        Assert.That(view.xMin,Is.LessThanOrEqualTo(occupied.xMin)); Assert.That(view.xMax,Is.GreaterThanOrEqualTo(occupied.xMax));
        Assert.That(view.yMin,Is.LessThanOrEqualTo(occupied.yMin)); Assert.That(view.yMax,Is.GreaterThanOrEqualTo(occupied.yMax));
    }
    [Test]
    public void TacticalViewportStaysInsideWorldFromCloseZoomThroughWholeMapForEveryDisplayShape()
    {
        var world = new Rect(-138,-120,276,240);
        foreach(float aspect in new[]{4f/3,16f/9,21f/9,32f/9})
        foreach(float requested in new[]{12f,48f,96f,138f,1000f})
        foreach(var wanted in new[]{world.center,world.min,world.max,new Vector2(10000,-10000)})
        {
            float zoom=TacticalMapMath.ClampViewSize(requested,world,aspect,12);
            var frame=TacticalMapMath.FrameCamera(world,zoom,aspect,new Rect(0,0,1,1));
            var center=TacticalMapMath.ClampViewCenter(wanted,world,frame.Size,frame.Aspect);
            var view=TacticalMapMath.CameraRect(center,frame.Size,frame.Aspect);
            Assert.That(view.xMin,Is.GreaterThanOrEqualTo(world.xMin-.001f));
            Assert.That(view.xMax,Is.LessThanOrEqualTo(world.xMax+.001f));
            Assert.That(view.yMin,Is.GreaterThanOrEqualTo(world.yMin-.001f));
            Assert.That(view.yMax,Is.LessThanOrEqualTo(world.yMax+.001f));
            Assert.That(frame.Viewport.xMin,Is.GreaterThanOrEqualTo(0));
            Assert.That(frame.Viewport.xMax,Is.LessThanOrEqualTo(1));
            Assert.That(frame.Viewport.yMin,Is.GreaterThanOrEqualTo(0));
            Assert.That(frame.Viewport.yMax,Is.LessThanOrEqualTo(1));
            Assert.That(frame.Aspect,Is.EqualTo(aspect*frame.Viewport.width/frame.Viewport.height).Within(.0001));
            if(requested==1000)Assert.That(Vector2.Distance(view.size,world.size),Is.LessThan(.001));
        }
    }
    [Test]
    public void EdgeEntryIncludesPlayerAndOccupiedAreaWhileKeepingCameraInsideNonSquareMap()
    {
        var world=new Rect(-160,-70,320,140);var player=new Vector2(149,-62);
        var occupied=new Rect(40,-70,120,70);
        foreach(float aspect in new[]{4f/3,16f/9,32f/9})
        {
            float zoom=TacticalMapMath.ClampViewSize(TacticalMapMath.FitAround(player,occupied,aspect,18),world,aspect,18);
            var frame=TacticalMapMath.FrameCamera(world,zoom,aspect,new Rect(0,0,1,1));
            var view=TacticalMapMath.CameraRect(TacticalMapMath.ClampViewCenter(player,world,frame.Size,frame.Aspect),frame.Size,frame.Aspect);
            Assert.That(view.Contains(player),Is.True);
            Assert.That(view.xMin,Is.LessThanOrEqualTo(occupied.xMin+.001));Assert.That(view.xMax,Is.GreaterThanOrEqualTo(occupied.xMax-.001));
            Assert.That(view.yMin,Is.LessThanOrEqualTo(occupied.yMin+.001));Assert.That(view.yMax,Is.GreaterThanOrEqualTo(occupied.yMax-.001));
        }
        Assert.That(float.IsFinite(TacticalMapMath.ClampViewSize(float.PositiveInfinity,world,0,float.NaN)),Is.True);
    }
    [Test]
    public void TacticalLeasesBlockCombatButKeepTurretCommandsSubjectToMenuLock()
    {
        var go=new GameObject("Control test");
        try
        {
            var control=go.AddComponent<PlayerControlState>();
            var first=control.EnterTacticalView(); var second=control.EnterTacticalView();
            Assert.That(control.CanMove||control.CanAct||control.CanPrepare,Is.False);
            Assert.That(control.CanIssueTurretCommands,Is.True);
            control.SetInputBlocked(true); Assert.That(control.CanIssueTurretCommands,Is.False);
            control.SetInputBlocked(false); first.Dispose(); first.Dispose();
            Assert.That(control.IsInTacticalView,Is.True); second.Dispose();
            Assert.That(control.IsInTacticalView,Is.False); Assert.That(control.CanAct,Is.False,"닫은 프레임의 지도 클릭은 발사로 전달하지 않습니다.");
            Assert.That(control.CanIssueTurretCommands,Is.True);
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test]
    public void AuthoredHudHasNoLegacyTurretPanelAndUsesMaskedNavigatorWithoutExtraCamera()
    {
        var hud=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab");
        var controller=hud.GetComponent<TacticalCommandController>(); Assert.That(controller,Is.Not.Null);
        Assert.That(hud.GetComponentInChildren<StageTurretControlView>(true),Is.Null);
        var v=controller.Style.ViewPrefab; Assert.That(v,Is.Not.Null);
        Assert.That(v.Overview.GetComponent<RectMask2D>(),Is.Not.Null);
        foreach(var graphic in v.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) Assert.That(graphic.GetComponent<CanvasRenderer>(),Is.Not.Null,graphic.name);
        Assert.That(v.GetComponentsInChildren<Camera>(true),Is.Empty); Assert.That(v.GetComponentsInChildren<AudioListener>(true),Is.Empty);
        Assert.That(v.ToggleTurret.GetComponent<TeamHJD.Game.Presentation.UiButtonFeedback>().PreserveLabelColor,Is.True);
        Assert.That(controller.Style.LowPower.Length,Is.GreaterThan(0)); Assert.That(controller.Style.Firepower.Length,Is.GreaterThan(0));
    }
}

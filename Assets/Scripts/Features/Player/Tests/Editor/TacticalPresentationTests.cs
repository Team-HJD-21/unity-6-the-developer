using System;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

public sealed class TacticalPresentationTests
{
    private static TurretPresentationSnapshot Row(TurretPresentationFlags flags,double cooling=0)
        => new(new EntityId("t"),new DefinitionId("canon"),"캐논",new BattlefieldPoint(0,0),1,10,10,1,10,12,cooling,default,0,flags);
    private const TurretPresentationFlags Ready=TurretPresentationFlags.Initialized;
    [Test]
    public void OccupiedRemoteAccessAndFieldCaptureStatusUseConfirmedCellsAndActualProgress()
    {
        Assert.That(TacticalPresentationRules.CanReach(true,true,true,200,10),Is.True);
        Assert.That(TacticalPresentationRules.CanReach(true,true,false,200,10),Is.False);
        Assert.That(TacticalPresentationRules.CanReach(true,true,false,2,10),Is.False,"지도는 근접해도 미점유 대상에 원격 조작을 열지 않습니다.");
        Assert.That(TacticalPresentationRules.CanReach(false,true,true,2,10),Is.False);
        Assert.That(TacticalPresentationRules.CanReach(true,false,false,2,10),Is.True);
        var grid=new BattlefieldGridConfiguration(new BattlefieldPoint(0,0),0,20,10,2,1);
        var definition=new RegionGridDefinition(grid,new[]{1,1},new[]{true,true});
        var snapshot=RegionGridSnapshot.FromCapturedCells(new MatchId("field"),Guid.NewGuid(),1,RegionPolicyMode.Static,definition,new[]{0},new[]{1});
        var inside=new BattlefieldPoint(-5,0);var outside=new BattlefieldPoint(5,0);
        Assert.That(TacticalPresentationRules.Field(snapshot,inside,null,false),Is.EqualTo(TacticalPresentationRules.FieldState.Hidden));
        Assert.That(TacticalPresentationRules.Field(snapshot,outside,null,false),Is.EqualTo(TacticalPresentationRules.FieldState.Outside));
        var capture=new StaticRegionCapture(1);capture.RecordDamage(20);capture.Tick(0,true,1,0,new StaticRegionCaptureSettings());capture.Tick(2,true,1,0,new StaticRegionCaptureSettings());
        Assert.That(TacticalPresentationRules.Field(snapshot,outside,capture.Snapshot,false),Is.EqualTo(TacticalPresentationRules.FieldState.Capturing));
        Assert.That(TacticalPresentationRules.Field(snapshot,outside,capture.Snapshot,true),Is.EqualTo(TacticalPresentationRules.FieldState.LeftCapture));
        capture.Tick(3,true,0,0,new StaticRegionCaptureSettings());
        Assert.That(TacticalPresentationRules.Field(snapshot,outside,capture.Snapshot,false),Is.EqualTo(TacticalPresentationRules.FieldState.Paused));
        Assert.That(TacticalPresentationRules.Field(null,outside,null,false),Is.EqualTo(TacticalPresentationRules.FieldState.Hidden));
    }
    [Test]
    public void ZeroHoldEnemyPressureAndUnopenedRegionsExplainWhyCaptureDoesNotStart()
    {
        var g=new BattlefieldGridConfiguration(new BattlefieldPoint(0,0),0,20,10,2,1);
        var d=new RegionGridDefinition(g,new[]{1,2},new[]{true,true});
        var s=RegionGridSnapshot.FromCapturedCells(new MatchId("pressure"),Guid.NewGuid(),1,RegionPolicyMode.Static,d,Array.Empty<int>(),new[]{1});
        var capture=new StaticRegionCapture(1);capture.RecordDamage(20);capture.Tick(0,true,1,3,new StaticRegionCaptureSettings());
        Assert.That(capture.HoldSeconds,Is.Zero);
        Assert.That(TacticalPresentationRules.Field(s,new BattlefieldPoint(-5,0),capture.Snapshot,false),Is.EqualTo(TacticalPresentationRules.FieldState.Contested));
        Assert.That(TacticalPresentationRules.Field(s,new BattlefieldPoint(5,0),null,false),Is.EqualTo(TacticalPresentationRules.FieldState.Locked));
        capture.Tick(1,true,1,2,new StaticRegionCaptureSettings());
        Assert.That(TacticalPresentationRules.Field(s,new BattlefieldPoint(-5,0),capture.Snapshot,false),Is.EqualTo(TacticalPresentationRules.FieldState.Capturing));
    }
    [Test]
    public void MapActionCaptionsReportLocksAndFailuresWhileOperationalTurretsKeepTheirActualState()
    {
        Assert.That(TacticalPresentationRules.ToggleCaption(Row(Ready),99,false,true),Is.EqualTo("잠김"));
        Assert.That(TacticalPresentationRules.ToggleCaption(Row(Ready),99,false,false),Is.EqualTo("켜기"));
        Assert.That(TacticalPresentationRules.ToggleCaption(Row(Ready),0,false,false),Is.EqualTo("전력 부족"));
        Assert.That(TacticalPresentationRules.ToggleCaption(Row(Ready|TurretPresentationFlags.Destroyed),99,false,true),Is.EqualTo("파괴됨"));
        Assert.That(TacticalPresentationRules.ToggleCaption(Row(Ready,2),99,false,false),Is.EqualTo("냉각"));
        Assert.That(TacticalPresentationRules.ToggleCaption(Row(Ready),99,true,false),Is.EqualTo("처리 중"));
        Assert.That(TacticalPresentationRules.State(Row(Ready),99),Is.EqualTo(TacticalPresentationRules.TurretState.Off));
        var active=Row(Ready|TurretPresentationFlags.Activated|TurretPresentationFlags.Operational);
        Assert.That(TacticalPresentationRules.State(active,99),Is.EqualTo(TacticalPresentationRules.TurretState.Active),"점유 상실이 실제 가동 상태까지 꺼짐으로 바꾸지는 않습니다.");
        Assert.That(TacticalPresentationRules.ToggleCaption(active,99,false,true),Is.EqualTo("잠김"));
    }
    [Test]
    public void AuthoredModeShaderKeepsTheSettledCenterTransparentAndDrawsOnlyTheRim()
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Requires a graphics device.");
        var source=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/UI/Tactical/TacticalModeFrame.mat");
        Assert.That(source,Is.Not.Null);Assert.That(UnityEditor.ShaderUtil.ShaderHasError(source.shader),Is.False);
        var material=new Material(source);var previous=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(320,180,0,RenderTextureFormat.ARGB32);
        var pixels=new Texture2D(320,180,TextureFormat.RGBA32,false);
        try
        {
            material.SetFloat("_Progress",1);material.SetFloat("_Cover",0);material.SetFloat("_Reveal",0);material.SetVector("_Resolution",new Vector4(320,180,0,0));
            RenderTexture.active=rt;GL.Clear(false,true,Color.clear);Graphics.Blit(Texture2D.whiteTexture,rt,material);
            RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,320,180),0,0);pixels.Apply();
            Assert.That(pixels.GetPixel(160,90).a,Is.LessThan(.001f),"유지 중 중앙 시야를 덮지 않습니다.");
            float rim=0;for(int y=0;y<180;y++)for(int x=0;x<12;x++)rim=Mathf.Max(rim,pixels.GetPixel(x,y).a);
            Assert.That(rim,Is.GreaterThan(.01f),"테두리의 실제 shader 결과가 있어야 합니다.");
            material.SetFloat("_Progress",0);material.SetFloat("_Cover",1);
            RenderTexture.active=rt;GL.Clear(false,true,Color.clear);Graphics.Blit(Texture2D.whiteTexture,rt,material);
            RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,320,180),0,0);pixels.Apply();
            int transparent=0;float maxAlpha=0;float minVisibleGreen=1;
            foreach(var pixel in pixels.GetPixels()){if(pixel.a<.01f)transparent++;maxAlpha=Mathf.Max(maxAlpha,pixel.a);if(pixel.a>.2f)minVisibleGreen=Mathf.Min(minVisibleGreen,pixel.g);}
            Assert.That(transparent,Is.GreaterThan(320*180*.7f),"전면 전환도 셀 내부의 게임 배경을 그대로 보여야 합니다.");
            Assert.That(maxAlpha,Is.InRange(.3f,.7f),"육각선은 보이되 불투명 검은 면을 만들지 않습니다.");
            Assert.That(minVisibleGreen,Is.GreaterThan(.2f));
            material.SetFloat("_Reveal",.5f);
            RenderTexture.active=rt;GL.Clear(false,true,Color.clear);Graphics.Blit(Texture2D.whiteTexture,rt,material);
            RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,320,180),0,0);pixels.Apply();
            Assert.That(pixels.GetPixel(160,90).a,Is.LessThan(.001f));
            float outer=0;for(int y=0;y<180;y++)for(int x=0;x<320;x++)if(x<24 || x>=296 || y<24 || y>=156)outer=Mathf.Max(outer,pixels.GetPixel(x,y).a);
            Assert.That(outer,Is.GreaterThan(.2f),"중앙이 걷혀도 바깥 육각선은 아직 남아야 합니다.");
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    [Test]
    public void ActualDamageStartsBriefImpactAndHealthTrailButBindingAndMaximumChangesDoNot()
    {
        var v=new TacticalPresentationRules.Vitality();v.Sample(70,100,0);
        Assert.That(v.Impact(0),Is.Zero);Assert.That(v.Trail(0),Is.EqualTo(.7f).Within(.001));
        v.Sample(40,100,1);Assert.That(v.Impact(1),Is.EqualTo(1));Assert.That(v.Trail(1.1f),Is.EqualTo(.7f).Within(.001));
        Assert.That(v.Offset(1.07f).sqrMagnitude,Is.GreaterThan(0));Assert.That(v.Offset(1.43f),Is.EqualTo(Vector2.zero));
        v.Sample(20,100,1.12f);Assert.That(v.Trail(1.13f),Is.GreaterThanOrEqualTo(.7f));
        Assert.That(v.Trail(2.1f),Is.EqualTo(.2f).Within(.001));
        v.Sample(200,300,3);Assert.That(v.Impact(3),Is.Zero);Assert.That(v.Trail(3),Is.EqualTo(2f/3).Within(.001));
        v.Sample(250,300,4);Assert.That(v.Impact(4),Is.Zero);
    }
    [Test]
    public void StatesDoNotMistakePoweredCoolingLockedOrUnaffordableTurretsForOff()
    {
        Assert.That(TacticalPresentationRules.State(Row(Ready|TurretPresentationFlags.Activated|TurretPresentationFlags.Operational),0),Is.EqualTo(TacticalPresentationRules.TurretState.Active));
        Assert.That(TacticalPresentationRules.State(Row(Ready),10),Is.EqualTo(TacticalPresentationRules.TurretState.Off));
        Assert.That(TacticalPresentationRules.State(Row(Ready),9),Is.EqualTo(TacticalPresentationRules.TurretState.NoPower));
        Assert.That(TacticalPresentationRules.State(Row(Ready|TurretPresentationFlags.Activated,2),99),Is.EqualTo(TacticalPresentationRules.TurretState.Cooling));
        Assert.That(TacticalPresentationRules.State(Row(Ready|TurretPresentationFlags.Locked),99),Is.EqualTo(TacticalPresentationRules.TurretState.Locked));
        Assert.That(TacticalPresentationRules.State(Row(Ready|TurretPresentationFlags.Destroyed|TurretPresentationFlags.Locked),99),Is.EqualTo(TacticalPresentationRules.TurretState.Destroyed));
        Assert.That(TacticalPresentationRules.State(Row(Ready),99,true),Is.EqualTo(TacticalPresentationRules.TurretState.Busy));
    }
    [Test]
    public void DragDirectionUsesTheActualViewportHeightAndDoesNotDependOnZoomOrScreenShape()
    {
        Assert.That(TacticalPresentationRules.DragPan(new Vector2(100,-50),20,800),Is.EqualTo(new Vector2(-5,2.5f)));
        Assert.That(TacticalPresentationRules.DragPan(new Vector2(100,-50),40,800),Is.EqualTo(new Vector2(-10,5)));
    }
    [Test]
    public void ConfirmedCellsUnlockEligibilityAndActualHoldingRemainDifferent()
    {
        var grid=new BattlefieldGridConfiguration(new BattlefieldPoint(0,0),0,40,10,4,1);
        var definition=new RegionGridDefinition(grid,new[]{1,1,2,1},new[]{true,true,true,false});
        var snapshot=RegionGridSnapshot.FromCapturedCells(new MatchId("t"),Guid.NewGuid(),1,RegionPolicyMode.Static,definition,new[]{0},new[]{1});
        Assert.That(TacticalPresentationRules.Cell(snapshot,0,null),Is.EqualTo(TacticalPresentationRules.CellState.Captured));
        Assert.That(TacticalPresentationRules.Cell(snapshot,1,null),Is.EqualTo(TacticalPresentationRules.CellState.Available));
        Assert.That(TacticalPresentationRules.Cell(snapshot,2,null),Is.EqualTo(TacticalPresentationRules.CellState.Unoccupied));
        Assert.That(TacticalPresentationRules.Cell(snapshot,3,null),Is.EqualTo(TacticalPresentationRules.CellState.Unoccupied));
        var progress=new StaticRegionCapture(1);progress.RecordDamage(20);progress.Tick(0,true,1,0,new StaticRegionCaptureSettings());
        progress.Tick(10,true,1,0,new StaticRegionCaptureSettings());
        Assert.That(TacticalPresentationRules.Cell(snapshot,1,progress.Snapshot),Is.EqualTo(TacticalPresentationRules.CellState.Capturing));
        Assert.That(TacticalPresentationRules.Cell(snapshot,0,progress.Snapshot),Is.EqualTo(TacticalPresentationRules.CellState.Captured));
        progress.Tick(11,true,0,0,new StaticRegionCaptureSettings());
        Assert.That(TacticalPresentationRules.Cell(snapshot,1,progress.Snapshot),Is.EqualTo(TacticalPresentationRules.CellState.Capturing),"실제로 유지되는 누적을 지우거나 UI가 완료로 바꾸지 않습니다.");
    }
}

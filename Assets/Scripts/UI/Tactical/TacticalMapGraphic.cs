using TeamHJD.Game.Domain;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>전체도는 공용 좌표를 그리며 별도 Camera/AudioListener/게임 원본을 만들지 않습니다.</summary>
[DefaultExecutionOrder(1100)]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TacticalMapGraphic : MaskableGraphic
{
    public TacticalHudView View;
    public bool Overview, Field;
    private TacticalCommandController Owner => View != null ? View.Owner : null;
    private VertexHelper _mesh;
    private RegionGridSnapshot _boundarySnapshot;
    private IReadOnlyList<RegionBoundarySegment> _boundary = Array.Empty<RegionBoundarySegment>();
    private void LateUpdate()
    {
        if (!Overview && !Field && Owner != null) Owner.RenderVisuals();
        if (Field) SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); _mesh = mesh; var owner = Owner;
        if (owner == null || owner.OutputCamera == null || (!Field && !owner.IsOpen)) return;
        if (Field)
        {
            if (owner.IsOpen) return;
            var hover = owner.Hovered;
            if (hover != null && View.ToggleMap.gameObject.activeSelf) Brackets(Project(TacticalCommandController.Point(hover)), 20, owner.Style.Common.Accent);
            return;
        }
        var regions = owner.Regions;

        DrawCaptureProgress();
        if(!Overview) DrawRanges();
        if (regions != null)
        {
            if (_boundarySnapshot == null || _boundarySnapshot.MatchId != regions.MatchId ||
                _boundarySnapshot.GridId != regions.GridId || _boundarySnapshot.Revision != regions.Revision ||
                !ReferenceEquals(_boundarySnapshot.Definition, regions.Definition))
            {
                _boundary = RegionBoundaryGeometry.Build(regions);
                _boundarySnapshot = regions;
            }
            // Field와 전체도 모두 동일한 확정 셀 외곽 좌표를 그대로 투영합니다.
            foreach (var edge in _boundary)
                Line(Project(edge.Start), Project(edge.End), Overview ? .8f : 1.1f, owner.Style.Territory);
        }
        foreach (var row in owner.Turrets)
        {
            Vector2 p = Project(TacticalCommandController.Point(row)); if (!rectTransform.rect.Overlaps(new Rect(p - Vector2.one * 25, Vector2.one * 50))) continue;
            p+=owner.HitOffset(row.EntityId)*(Overview?.38f:PixelScale);
            var status=owner.StateOf(row);
            Color state = owner.StateColor(status);
            float glyphScale=Overview?1:1/Mathf.Min(1,Mathf.Max(.1f,View.Canvas.rootCanvas.scaleFactor));
            if (!Overview && owner.HasTurretGlyph(row.EntityId))
            {
                DrawStatus(p + Vector2.down * (24*glyphScale),status,state,glyphScale);
            }
            else if(Overview) DrawStatus(p,status,state,.52f);
            else DrawStatus(p+Vector2.down*24*glyphScale,status,state,glyphScale);
            if (owner.IsSelected(row.EntityId)) Brackets(p, Overview ? 7 : owner.HasTurretGlyph(row.EntityId)?19*glyphScale:18, owner.Style.Common.Accent);
            else if(!Overview && owner.Hovered?.EntityId==row.EntityId)Brackets(p,owner.HasTurretGlyph(row.EntityId)?19*glyphScale:18,owner.Style.Common.Text);
            if (!Overview && (row.CurrentHealth<row.MaxHealth || owner.IsSelected(row.EntityId) || owner.WasHit(row.EntityId)))
            {
                Vector2 hp=p+Vector2.down*(38*glyphScale);
                Health(hp,42*glyphScale,4*glyphScale,row.CurrentHealth/(float)Mathf.Max(1,row.MaxHealth),owner.HealthTrail(row));
            }
            float impact=owner.HitImpact(row.EntityId);
            if(impact>0){var flash=owner.Style.Enemy;flash.a=impact;Brackets(p,Overview?10:24*glyphScale,flash);}

        }
        if (Overview)
        {
            foreach (var p in owner.Enemies) Quad(Project(p) - Vector2.one * 1.5f, Project(p) + Vector2.one * 1.5f, owner.Style.Enemy);
            var player = Project(owner.PlayerPosition); Circle(player, 5, 1.5f, owner.Style.Common.Text, 12);
            if (owner.TryControlPosition(out var cu)) Brackets(Project(cu), 5, owner.Style.Common.Power);
            Rect camera = owner.CurrentCameraRect;
            Vector2 a = Project(camera.min), b = Project(camera.max);
            Frame(a, b, 1.5f, owner.Style.Common.Text);
            if (owner.HasGoal) Brackets(Project(owner.GoalPosition), 5, owner.Style.Common.Accent);
        }
        else
        {
            if(owner.ShowEnemyGlyphs)
                foreach(var enemy in owner.Enemies)
                {var p=Project(enemy);if(rectTransform.rect.Contains(p))Disc(p,3*PixelScale,owner.Style.Enemy);}
            if(owner.HasGoal) Brackets(Project(owner.GoalPosition),12*PixelScale,owner.Style.Common.Accent);
            var player=owner.PlayerVitality;var playerPoint=Project(owner.PlayerPosition)+player.Offset(Time.unscaledTime)*PixelScale;
            if(player.Maximum>0)Health(playerPoint+Vector2.down*20*PixelScale,36*PixelScale,4*PixelScale,player.Fraction,player.Trail(Time.unscaledTime));
            if(owner.TryControlPosition(out var cu))
            {
                var vitality=owner.ControlVitality;var p=Project(cu)+vitality.Offset(Time.unscaledTime)*PixelScale;
                if(vitality.Maximum>0)Health(p+Vector2.down*20*PixelScale,48*PixelScale,5*PixelScale,vitality.Fraction,vitality.Trail(Time.unscaledTime));
                if(vitality.Impact(Time.unscaledTime)>0)Brackets(p,24*PixelScale,owner.Style.Enemy);
            }
        }
    }
    private float PixelScale => 1/Mathf.Min(1,Mathf.Max(.1f,View.Canvas.rootCanvas.scaleFactor));
    private void Health(Vector2 p,float width,float height,float fraction,float trail)
    {
        var half=new Vector2(width*.5f,height*.5f);
        Quad(p-half-Vector2.one*PixelScale,p+half+Vector2.one*PixelScale,new Color(.025f,.04f,.035f,.98f));
        Quad(p-half,p+half,new Color(.18f,.22f,.19f,.98f));
        Quad(p-half,p+new Vector2(-half.x+width*Mathf.Clamp01(trail),half.y),Owner.Style.Enemy);
        var tone=fraction<.3f?Owner.Style.Enemy:fraction<.6f?Owner.Style.Common.Power:Owner.Style.Active;
        Quad(p-half,p+new Vector2(-half.x+width*Mathf.Clamp01(fraction),half.y),tone);
        for(int n=1;n<4;n++){float x=p.x-half.x+width*n/4;Line(new Vector2(x,p.y-half.y),new Vector2(x,p.y+half.y),PixelScale,new Color(.025f,.04f,.035f,.75f));}
    }
    private void DrawCaptureProgress()
    {
        foreach(var capture in Owner.CaptureMarkers)
        {
            Vector2 p=Project(capture.Position);float radius=Overview?7:18*PixelScale,w=Overview?2:3*PixelScale;
            Circle(p,radius,w+2*PixelScale,new Color(.025f,.04f,.035f,.94f),32);
            Arc(p,radius,w,Owner.Style.Common.Power,-90,-90+360*capture.Progress,40);
            if(!capture.Holding){Line(p+new Vector2(-3,-4)*PixelScale,p+new Vector2(-3,4)*PixelScale,2*PixelScale,Owner.Style.Common.Power);Line(p+new Vector2(3,-4)*PixelScale,p+new Vector2(3,4)*PixelScale,2*PixelScale,Owner.Style.Common.Power);}
            else if(!Overview)Brackets(p,8*PixelScale,Owner.Style.Common.Power);
        }
    }
    private void Arc(Vector2 p,float radius,float width,Color c,float from,float to,int steps)
    {
        for(int i=0;i<steps;i++){float a=Mathf.Lerp(from,to,i/(float)steps)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(i+1)/(float)steps)*Mathf.Deg2Rad;Line(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,c);}
    }
    private void DrawRanges()
    {
        foreach(var row in Owner.Turrets)
        {
            bool selected=Owner.IsSelected(row.EntityId);
            if(!(selected || Owner.ShowAllRanges && row.IsActivated && !row.IsDestroyed))continue;
            var p=Project(TacticalCommandController.Point(row));
            var edge=Project(TacticalCommandController.Point(row)+Vector2.right*(float)row.Range);
            float radius=Vector2.Distance(p,edge);if(radius<=0 || !rectTransform.rect.Overlaps(new Rect(p-Vector2.one*radius,Vector2.one*radius*2)))continue;
            var c=selected?Owner.Style.Common.Accent:Owner.StateColor(Owner.StateOf(row));c.a=selected?.85f:.4f;
            Circle(p,radius,(selected?3.4f:2.6f)*PixelScale,new Color(.03f,.045f,.04f,.62f),64);
            Circle(p,radius,(selected?1.8f:1.1f)*PixelScale,c,64);
        }
    }
    private void DrawStatus(Vector2 p,TacticalPresentationRules.TurretState status,Color c,float scale)
    {
        Vector2 S(float x,float y)=>p+new Vector2(x,y)*scale;
        Quad(S(-9,-8),S(9,8),new Color(.035f,.05f,.045f,.94f));
        float w=1.7f*scale;
        switch(status)
        {
            case TacticalPresentationRules.TurretState.Active:
                Circle(p,5*scale,w,c,12);Line(S(0,1),S(0,7),w,c);Disc(S(6,5),2*scale,c);break;
            case TacticalPresentationRules.TurretState.Off:
                Circle(p,5*scale,w,c,12);Line(S(-3,0),S(3,0),w,c);break;
            case TacticalPresentationRules.TurretState.Locked:
                Frame(S(-5,-6),S(5,1),w,c);Line(S(-3,1),S(-3,5),w,c);Line(S(-3,5),S(3,5),w,c);Line(S(3,5),S(3,1),w,c);break;
            case TacticalPresentationRules.TurretState.Cooling:
                Circle(p,6*scale,w,c,12);Line(p,S(0,4),w,c);Line(p,S(3,-2),w,c);break;
            case TacticalPresentationRules.TurretState.NoPower:
                Line(S(2,6),S(-3,0),w,c);Line(S(-3,0),S(3,0),w,c);Line(S(3,0),S(-2,-6),w,c);
                Line(S(-7,6),S(-7,-2),w,c);Disc(S(-7,-5),scale,c);break;
            case TacticalPresentationRules.TurretState.Destroyed:
                Line(S(-5,-5),S(5,5),w,c);Line(S(-5,5),S(5,-5),w,c);break;
            default:
                Quad(S(-4,-4),S(-1,4),c);Quad(S(1,-4),S(4,4),c);break;
        }
    }
    private void Disc(Vector2 p,float radius,Color c)
    {
        if(_mesh.currentVertCount>59940)return;
        int first=_mesh.currentVertCount;_mesh.AddVert(p,c,Vector2.zero);
        for(int n=0;n<=12;n++){float t=n*Mathf.PI*2/12;_mesh.AddVert(p+new Vector2(Mathf.Cos(t),Mathf.Sin(t))*radius,c,Vector2.zero);if(n>0)_mesh.AddTriangle(first,first+n,first+n+1);}
    }
    private Vector2 Project(Vector2 p)
    {
        if (Overview) return TacticalMapMath.Project(p, Owner.WorldBounds, rectTransform.rect);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,Owner.OutputCamera.WorldToScreenPoint(p),null,out var point);
        return point;
    }
    private void Line(Vector2 a, Vector2 b, float width, Color c)
    {
        if (_mesh.currentVertCount > 59990 || !float.IsFinite(a.x + a.y + b.x + b.y)) return;
        Vector2 normal = (b - a).normalized; normal = new Vector2(-normal.y, normal.x) * width * .5f;
        int i = _mesh.currentVertCount; _mesh.AddVert(a - normal, c, Vector2.zero); _mesh.AddVert(a + normal, c, Vector2.zero);
        _mesh.AddVert(b + normal, c, Vector2.zero); _mesh.AddVert(b - normal, c, Vector2.zero);
        _mesh.AddTriangle(i, i + 1, i + 2); _mesh.AddTriangle(i, i + 2, i + 3);
    }
    private void Quad(Vector2 a, Vector2 b, Color c)
    {
        if (_mesh.currentVertCount > 59990) return;
        int i = _mesh.currentVertCount; _mesh.AddVert(a, c, Vector2.zero); _mesh.AddVert(new Vector2(a.x, b.y), c, Vector2.zero);
        _mesh.AddVert(b, c, Vector2.zero); _mesh.AddVert(new Vector2(b.x, a.y), c, Vector2.zero);
        _mesh.AddTriangle(i, i + 1, i + 2); _mesh.AddTriangle(i, i + 2, i + 3);
    }
    private void Frame(Vector2 a, Vector2 b, float w, Color c)
    { Line(a, new Vector2(a.x, b.y), w, c); Line(new Vector2(a.x, b.y), b, w, c); Line(b, new Vector2(b.x, a.y), w, c); Line(new Vector2(b.x, a.y), a, w, c); }
    private void Brackets(Vector2 p, float r, Color c)
    {
        foreach (var sign in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
        { Vector2 a = p + sign * r; Line(a, a - new Vector2(sign.x * r * .4f, 0), 1.6f, c); Line(a, a - new Vector2(0, sign.y * r * .4f), 1.6f, c); }
    }
    private void Circle(Vector2 p, float radius, float width, Color c, int steps)
    {
        Vector2 previous = p + Vector2.right * radius;
        for (int i = 1; i <= steps; i++) { float t = i * Mathf.PI * 2 / steps; Vector2 next = p + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius; Line(previous, next, width, c); previous = next; }
    }
}

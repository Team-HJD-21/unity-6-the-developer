using TeamHJD.Game.Domain;
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
        if (regions != null && !Overview)
        {
            var g = regions.Definition.Geometry;
            // 채움과 권한은 확정 셀을 사용합니다. 삼각형 외곽을 점유 경계로 대체하지 않습니다.
            for (int y = 0; y < g.CellsY; y++) for (int x = 0; x < g.CellsX; x++)
            {
                if (!regions.IsCaptured(x, y)) continue;
                var a = new Vector2((float)(g.MinimumX + x * g.CellWidth), (float)(g.MinimumY + y * g.CellHeight));
                var b = a + new Vector2((float)g.CellWidth, (float)g.CellHeight);
                var bounds = owner.CurrentCameraRect;
                if (b.x < bounds.xMin || a.x > bounds.xMax || b.y < bounds.yMin || a.y > bounds.yMax) continue;
                if (x == 0 || !regions.IsCaptured(x - 1, y)) Line(Project(a), Project(new Vector2(a.x, b.y)), 1.1f, owner.Style.Territory);
                if (y == 0 || !regions.IsCaptured(x, y - 1)) Line(Project(a), Project(new Vector2(b.x, a.y)), 1.1f, owner.Style.Territory);
                if (x == g.CellsX - 1 || !regions.IsCaptured(x + 1, y)) Line(Project(new Vector2(b.x, a.y)), Project(b), 1.1f, owner.Style.Territory);
                if (y == g.CellsY - 1 || !regions.IsCaptured(x, y + 1)) Line(Project(new Vector2(a.x, b.y)), Project(b), 1.1f, owner.Style.Territory);
            }
        }
        foreach (var row in owner.Turrets)
        {
            Vector2 p = Project(TacticalCommandController.Point(row)); if (!rectTransform.rect.Overlaps(new Rect(p - Vector2.one * 25, Vector2.one * 50))) continue;
            Color state = row.IsDestroyed ? owner.Style.Common.Health : row.IsOperational ? owner.Style.Active : owner.Style.Inactive;
            float radius = Overview ? 3 : 4;
            float glyphScale=Overview?1:1/Mathf.Min(1,Mathf.Max(.1f,View.Canvas.rootCanvas.scaleFactor));
            if (!Overview && owner.HasTurretGlyph(row.EntityId))
            {
                Vector2 status = p + Vector2.down * (21*glyphScale);
                if (row.IsDestroyed) { Line(status + new Vector2(-3,-3),status + new Vector2(3,3),1.2f,state); Line(status + new Vector2(-3,3),status + new Vector2(3,-3),1.2f,state); }
                else if (row.IsLocked) { Frame(status - new Vector2(3,2),status + new Vector2(3,2),1.2f,state); Circle(status + Vector2.up * 3,2,1.2f,state,8); }
                else if (row.IsOperational) Quad(status - new Vector2(4,1.5f),status + new Vector2(4,1.5f),state);
                else Frame(status - new Vector2(4,1.5f),status + new Vector2(4,1.5f),1,state);
            }
            else if (row.IsOperational) Quad(p - Vector2.one * radius, p + Vector2.one * radius, state);
            else Circle(p, radius + 1, 1.2f, state, 8);
            if (owner.IsSelected(row.EntityId)) Brackets(p, Overview ? 7 : owner.HasTurretGlyph(row.EntityId)?19*glyphScale:18, owner.Style.Common.Accent);
            else if(!Overview && owner.Hovered?.EntityId==row.EntityId)Brackets(p,owner.HasTurretGlyph(row.EntityId)?19*glyphScale:18,owner.Style.Common.Text);
            if (!Overview && owner.WasHit(row.EntityId))
            {
                Vector2 hp = p + Vector2.down * (owner.HasTurretGlyph(row.EntityId) ? 36*glyphScale : 25);
                Quad(hp - new Vector2(23, 2), hp + new Vector2(23, 2), new Color(.08f, .1f, .1f, .8f));
                Quad(hp - new Vector2(23, 2), hp + new Vector2(-23 + 46f * row.CurrentHealth / Mathf.Max(1, row.MaxHealth), 2), owner.Style.Common.Health);
            }
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
        else if (owner.SelectionCount==1 && owner.Selected != null)
        {
            var row = owner.Selected; Vector2 p = Project(TacticalCommandController.Point(row));
            Vector2 edge = Project(TacticalCommandController.Point(row) + Vector2.right * (float)row.Range);
            var c = owner.Style.Common.Accent; c.a = .38f; Circle(p, Vector2.Distance(p, edge), 1.1f, c, 96);
        }
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

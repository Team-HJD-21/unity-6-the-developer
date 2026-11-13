using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD의 얇은 기계식 윤곽입니다. 원본 그림·폰트와 독립된 편집 가능한 UGUI 메시입니다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CombatHudShape : MaskableGraphic
{
    public enum Form { Needle, KeyBadge, HeaderPlate, Grip, SlotSilhouette }
    public Form Shape;
    public Color Face = new(.055f, .065f, .07f, .97f);
    [Min(.5f)] public float Stroke = 1.5f;
    public float KeyRadius = 100;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        switch (Shape)
        {
            case Form.Needle:
                float y = r.yMin;
                Line(mesh, new(0, y + 8), new(0, y + KeyRadius - 16.5f), color);
                Line(mesh, new(0, y + KeyRadius + 16.5f), new(0, y + 181), color);
                Tri(mesh, new(0, y + 193), new(-5, y + 179), new(5, y + 179), color);
                break;
            case Form.KeyBadge:
                float radius = Mathf.Min(r.width, r.height) * .5f - 1;
                for (int i = 0; i < 48; i++)
                {
                    Vector2 a = r.center + Dir(i * 7.5f) * radius, b = r.center + Dir((i + 1) * 7.5f) * radius;
                    Tri(mesh, r.center, a, b, Face);
                    Line(mesh, a, b, color);
                }
                break;
            case Form.HeaderPlate:
                Vector2 tl = new(r.xMin, r.yMax), tr = new(r.xMax, r.yMax);
                Vector2 bl = new(r.xMin + 25, r.yMin), br = new(r.xMax - 25, r.yMin);
                Tri(mesh, tl, tr, br, Face); Tri(mesh, tl, br, bl, Face);
                Line(mesh, tl, bl, color); Line(mesh, bl, br, color); Line(mesh, br, tr, color);
                break;
            case Form.Grip:
                for (int x = 0; x < 2; x++) for (int row = 0; row < 3; row++)
                {
                    Vector2 p = r.center + new Vector2(x * 5 - 2.5f, row * 5 - 5);
                    Tri(mesh, p + new Vector2(-1, -1), p + new Vector2(-1, 1), p + new Vector2(1, 1), color);
                    Tri(mesh, p + new Vector2(-1, -1), p + new Vector2(1, 1), p + new Vector2(1, -1), color);
                }
                break;
            case Form.SlotSilhouette:
                // Solid polygon keeps cooldown ink inside the frame; soft sprite shadows are not a stencil silhouette.
                r.xMin += 2; r.xMax -= 2; r.yMin += 2; r.yMax -= 2;
                const float cut = 5;
                Vector2[] points = { new(r.xMin + cut, r.yMax), new(r.xMax - cut, r.yMax),
                    new(r.xMax, r.yMax - cut), new(r.xMax, r.yMin + cut), new(r.xMax - cut, r.yMin),
                    new(r.xMin + cut, r.yMin), new(r.xMin, r.yMin + cut), new(r.xMin, r.yMax - cut) };
                for (int i = 0; i < points.Length; i++) Tri(mesh, r.center, points[i], points[(i + 1) % points.Length], color);
                break;
        }
    }
    private static Vector2 Dir(float degrees) => new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
    private void Line(VertexHelper mesh, Vector2 a, Vector2 b, Color tint)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < .0001f) return;
        Vector2 n = new Vector2(-d.y, d.x).normalized * Stroke * .5f;
        Tri(mesh, a - n, a + n, b + n, tint); Tri(mesh, a - n, b + n, b - n, tint);
    }
    private static void Tri(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int at = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero); mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddTriangle(at, at + 1, at + 2);
    }
}

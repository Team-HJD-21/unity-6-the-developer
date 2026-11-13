using UnityEngine;
using UnityEngine.UI;

/// <summary>단순 잠금/재장전 심볼을 같은 굵기의 UI 벡터로 그립니다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CombatHudSymbol : MaskableGraphic
{
    public enum Shape { Lock, Reload }
    public Shape Symbol;
    [Range(.5f, 4)] public float Stroke = 1.8f;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        float size = Mathf.Min(rect.width, rect.height);
        Vector2 center = rect.center;
        if (Symbol == Shape.Lock)
        {
            // Body outline plus a curved shackle; no pin head or decorative glow.
            float half = size * .28f, bottom = -size * .36f, top = size * .05f;
            Line(mesh, center + new Vector2(-half, bottom), center + new Vector2(half, bottom));
            Line(mesh, center + new Vector2(half, bottom), center + new Vector2(half, top));
            Line(mesh, center + new Vector2(half, top), center + new Vector2(-half, top));
            Line(mesh, center + new Vector2(-half, top), center + new Vector2(-half, bottom));
            Arc(mesh, center + Vector2.up * size * .1f, size * .19f, 0, 180, 16);
            Line(mesh, center + new Vector2(-size * .19f, size * .1f), center + new Vector2(-size * .19f, top));
            Line(mesh, center + new Vector2(size * .19f, size * .1f), center + new Vector2(size * .19f, top));
            Line(mesh, center + new Vector2(0, -size * .12f), center + new Vector2(0, -size * .23f));
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                float end = 20 + 180 * i;
                Arc(mesh, center, size * .32f, end + 132, end, 28);
                Vector2 point = center + Direction(end) * (size * .32f);
                Vector2 tangent = new(Mathf.Sin(end * Mathf.Deg2Rad), -Mathf.Cos(end * Mathf.Deg2Rad));
                Vector2 normal = new(-tangent.y, tangent.x);
                Triangle(mesh, point + tangent * size * .065f, point - tangent * size * .08f + normal * size * .075f,
                    point - tangent * size * .08f - normal * size * .075f);
            }
        }
    }
    private static Vector2 Direction(float angle) => new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    private void Arc(VertexHelper mesh, Vector2 center, float radius, float from, float to, int steps)
    {
        for (int i = 0; i < steps; i++)
            Line(mesh, center + Direction(Mathf.Lerp(from, to, (float)i / steps)) * radius,
                center + Direction(Mathf.Lerp(from, to, (float)(i + 1) / steps)) * radius);
    }
    private void Line(VertexHelper mesh, Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude < .00001f) return;
        Vector2 side = new Vector2(-direction.y, direction.x).normalized * (Stroke * .5f);
        int at = mesh.currentVertCount;
        mesh.AddVert(from - side, color, Vector2.zero); mesh.AddVert(from + side, color, Vector2.zero);
        mesh.AddVert(to + side, color, Vector2.zero); mesh.AddVert(to - side, color, Vector2.zero);
        mesh.AddTriangle(at, at + 1, at + 2); mesh.AddTriangle(at, at + 2, at + 3);
    }
    private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
    {
        int at = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero); mesh.AddVert(c, color, Vector2.zero);
        mesh.AddTriangle(at, at + 1, at + 2);
    }
}

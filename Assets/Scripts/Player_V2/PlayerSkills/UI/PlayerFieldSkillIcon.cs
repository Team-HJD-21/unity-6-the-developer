using UnityEngine;
using UnityEngine.UI;

/// <summary>방벽과 비콘을 해상도에 독립적인 uGUI 도형으로 표시합니다.</summary>
public sealed class PlayerFieldSkillIcon : MaskableGraphic
{
    private PlayerSkillId _skill;
    public void SetSkill(PlayerSkillId skill)
    {
        if (_skill == skill) return;
        _skill = skill;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        var rect = GetPixelAdjustedRect();
        var scale = Mathf.Min(rect.width, rect.height);
        var center = rect.center;
        var tint = color * (_skill == PlayerSkillId.EnergyBarrier ? new Color(.25f, .95f, 1) : new Color(1, .7f, .2f));
        if (_skill == PlayerSkillId.EnergyBarrier)
        {
            Quad(vertices, center, scale, new Vector2(-.3f, -.24f), new Vector2(.3f, .22f), new Color(tint.r, tint.g, tint.b, tint.a * .3f));
            Stroke(vertices, center, scale, new Vector2(-.3f, -.24f), new Vector2(.3f, -.24f), .045f, tint);
            Stroke(vertices, center, scale, new Vector2(-.3f, .22f), new Vector2(.3f, .22f), .045f, tint);
            foreach (float x in new[] { -.33f, .33f })
            {
                Quad(vertices, center, scale, new Vector2(x - .06f, -.32f), new Vector2(x + .06f, .3f), tint);
                Ring(vertices, center + new Vector2(x, -.3f) * scale, scale * .105f, scale * .025f, tint);
            }
        }
        else if (_skill == PlayerSkillId.LureBeacon)
        {
            Ring(vertices, center, scale * .38f, scale * .025f, tint);
            Ring(vertices, center, scale * .25f, scale * .025f, tint);
            Quad(vertices, center, scale, new Vector2(-.07f, -.2f), new Vector2(.07f, .2f), tint);
            Quad(vertices, center, scale, new Vector2(-.2f, -.3f), new Vector2(.2f, -.22f), tint);
            Ring(vertices, center + Vector2.up * scale * .2f, scale * .08f, scale * .04f, Color.white * color);
        }
    }

    private static void Quad(VertexHelper v, Vector2 center, float scale, Vector2 min, Vector2 max, Color tint)
    {
        int first = v.currentVertCount;
        v.AddVert(center + new Vector2(min.x, min.y) * scale, tint, Vector2.zero);
        v.AddVert(center + new Vector2(min.x, max.y) * scale, tint, Vector2.zero);
        v.AddVert(center + new Vector2(max.x, max.y) * scale, tint, Vector2.zero);
        v.AddVert(center + new Vector2(max.x, min.y) * scale, tint, Vector2.zero);
        v.AddTriangle(first, first + 1, first + 2);
        v.AddTriangle(first, first + 2, first + 3);
    }

    private static void Stroke(VertexHelper v, Vector2 center, float scale, Vector2 a, Vector2 b, float width, Color tint)
    {
        var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
        int first = v.currentVertCount;
        v.AddVert(center + (a - normal) * scale, tint, Vector2.zero);
        v.AddVert(center + (a + normal) * scale, tint, Vector2.zero);
        v.AddVert(center + (b + normal) * scale, tint, Vector2.zero);
        v.AddVert(center + (b - normal) * scale, tint, Vector2.zero);
        v.AddTriangle(first, first + 1, first + 2);
        v.AddTriangle(first, first + 2, first + 3);
    }

    private static void Ring(VertexHelper v, Vector2 center, float radius, float width, Color tint)
    {
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16;
            Stroke(v, center, 1, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, tint);
        }
    }
}

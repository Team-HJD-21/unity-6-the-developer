using UnityEngine;
using UnityEngine.UI;

/// <summary>중앙 시야를 비워 둔 붉은 화면 가장자리입니다. 텍스처나 런타임 UI 객체를 만들지 않습니다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
public sealed class PlayerDamageVignette : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        Vector2 half = rect.size * .5f;
        if (half.x <= 0 || half.y <= 0) return;
        const int segments = 64;
        Color clear = color; clear.a = 0;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float edge = 1f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            mesh.AddVert(rect.center + Vector2.Scale(direction, half) * .55f, clear, Vector2.zero);
            mesh.AddVert(rect.center + Vector2.Scale(direction, half) * edge, color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
        {
            int next = ((i + 1) % segments) * 2;
            mesh.AddTriangle(i * 2, i * 2 + 1, next + 1);
            mesh.AddTriangle(i * 2, next + 1, next);
        }
    }
}

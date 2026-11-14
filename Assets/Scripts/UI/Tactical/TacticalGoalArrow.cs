using UnityEngine;
using UnityEngine.UI;

/// <summary>목표 방향을 가리키는 단색 셰브론. 원본 크기에 상관없이 선명한 UI 메시입니다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TacticalGoalArrow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); Rect r = rectTransform.rect;
        Vector2[] p = { new(0, r.yMax), new(r.xMax * .8f, r.yMin), new(0, r.yMin * .45f), new(r.xMin * .8f, r.yMin) };
        foreach (var v in p) vh.AddVert(v, color, Vector2.zero);
        vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
    }
}

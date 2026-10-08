using UnityEngine;

/// <summary>장비와 설치 미리보기에서 사용하는 선 렌더러를 생성합니다.</summary>
public static class PlayerFieldLineFactory
{
    public static LineRenderer Line(string name, Material material, float width, Color color, Transform parent = null)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        if (parent != null) line.transform.SetParent(parent, false);
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.numCapVertices = 4;
        line.sortingOrder = 100;
        return line;
    }
    public static void SetLine(LineRenderer line, Vector3 start, Vector3 end)
    {
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }
    public static void Circle(LineRenderer line, Vector3 center, float radius, int segments = 48)
    {
        line.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
    }
}

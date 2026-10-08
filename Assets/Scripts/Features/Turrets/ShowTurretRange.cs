using UnityEngine;

public class TowerRangeVisualizer : MonoBehaviour
{
    public float outerRadius = 5f; // 외부 반지름 (최대 사거리)
    public float innerRadius = 2f; // 내부 반지름 (최소 사거리)
    public int segments = 64; // 원형 세그먼트 수
    public Color rangeColor = new Color(0, 1, 0, 0.5f); // 반투명한 초록색

    private LineRenderer outerRangeRenderer;
    private LineRenderer innerRangeRenderer;

    void Start()
    {
        // 외부 범위
        outerRangeRenderer = CreateCircle(outerRadius);
        outerRangeRenderer.startColor = rangeColor;
        outerRangeRenderer.endColor = rangeColor;

        // 내부 범위
        innerRangeRenderer = CreateCircle(innerRadius);
        innerRangeRenderer.startColor = Color.black; // 내부 범위를 다른 색으로
        innerRangeRenderer.endColor = Color.black;
    }

    LineRenderer CreateCircle(float radius)
    {
        GameObject obj = new GameObject("CircleRenderer");
        obj.transform.parent = transform;

        LineRenderer lineRenderer = obj.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.startWidth = 0.05f; // 선 두께
        lineRenderer.endWidth = 0.05f;

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.positionCount = segments;

        Vector3[] points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        lineRenderer.SetPositions(points);
        return lineRenderer;
    }
}
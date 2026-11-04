using UnityEngine;

/// <summary>커서 거리와 관계없이 플레이어 중심의 주변 8칸을 선택합니다.</summary>
public static class PlayerSkillPlacementUtility
{
    public static Vector3 GetPosition(Vector3 origin, Vector2 pointer, Vector2 fallback, float cellSize)
    {
        Vector2 direction = pointer - (Vector2)origin;
        if (!PlayerAimUtility.IsValidDirection(direction)) direction = PlayerAimUtility.IsValidDirection(fallback) ? fallback : Vector2.down;
        float angle = Mathf.Round(Mathf.Atan2(direction.y, direction.x) / (Mathf.PI / 4f)) * (Mathf.PI / 4f);
        return origin + new Vector3(Mathf.RoundToInt(Mathf.Cos(angle)), Mathf.RoundToInt(Mathf.Sin(angle)), 0) * Mathf.Max(.1f, cellSize);
    }

    public static bool IsFinite(Vector3 point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);

    /// <summary>회전하지 않는 각 본체 그림과 편집된 콜라이더를 합쳐 검사합니다.</summary>
    public static bool TryGetDeviceFootprint(PlayerFieldDevice prefab, Vector3 position, int emitterIndex, out Bounds footprint)
    {
        footprint = default;
        if (prefab == null || prefab.EmitterSprite == null || !IsFinite(position)) return false;
        var scale = Vector3.Scale(prefab.EmitterScale, prefab.transform.localScale);
        var visual = prefab.EmitterSprite.bounds;
        footprint = new Bounds(position + Vector3.Scale(visual.center, scale),
            new Vector3(Mathf.Abs(visual.size.x * scale.x), Mathf.Abs(visual.size.y * scale.y), 0));
        var bodies = prefab.GetComponent<PlayerDeviceBodyCollision>()?.Bodies;
        if (bodies == null || emitterIndex >= bodies.Count) return false;
        var body = bodies[emitterIndex];
        if (body == null || !body.enabled || body.isTrigger) return false;
        Vector3 bodyScale = body.transform.lossyScale;
        if (body is BoxCollider2D box)
            footprint.Encapsulate(new Bounds(position + Vector3.Scale(box.offset, bodyScale),
                new Vector3(Mathf.Abs(box.size.x * bodyScale.x), Mathf.Abs(box.size.y * bodyScale.y), 0)));
        else if (body is PolygonCollider2D polygon)
        {
            for (int path = 0; path < polygon.pathCount; path++)
            foreach (var point in polygon.GetPath(path))
                footprint.Encapsulate(position + Vector3.Scale(point + polygon.offset, bodyScale));
        }
        else return false;
        return footprint.size.x > 0 && footprint.size.y > 0;
    }
}

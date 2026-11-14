using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>발밑 영역이 육지에 남도록 걷기·대시의 이동 경로를 검사합니다.</summary>
public static class PlayerGroundMovement
{
    public static Vector2 ConstrainVelocity(Tilemap ground, Bounds feet, Vector2 velocity, float seconds)
    {
        if (ground == null || !ground.isActiveAndEnabled || seconds <= 0) return velocity;
        Vector2 delta = velocity * seconds;
        if (delta.sqrMagnitude <= 0) return velocity;
        Vector2 travel = Sweep(ground, feet, delta);
        if ((travel - delta).sqrMagnitude < .00000001f) return velocity;

        // 해안을 비스듬히 밀 때 유효한 축 방향 이동은 유지합니다.
        Vector2 horizontal = Sweep(ground, feet, new Vector2(delta.x, 0));
        Vector2 vertical = Sweep(ground, feet, new Vector2(0, delta.y));
        if (horizontal.sqrMagnitude > travel.sqrMagnitude) travel = horizontal;
        if (vertical.sqrMagnitude > travel.sqrMagnitude) travel = vertical;
        return travel / seconds;
    }

    private static Vector2 Sweep(Tilemap ground, Bounds feet, Vector2 delta)
    {
        // 끝점만 검사하면 대시가 좁은 바다를 건너뛸 수 있으므로 셀보다 짧게 나눕니다.
        Vector3 cell = ground.layoutGrid != null ? ground.layoutGrid.cellSize : ground.cellSize;
        Vector3 scale = ground.transform.lossyScale;
        float stride = Mathf.Max(.01f, Mathf.Min(Mathf.Abs(cell.x * scale.x), Mathf.Abs(cell.y * scale.y)) * .25f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / stride));
        float previous = 0;
        for (int i = 1; i <= steps; i++)
        {
            float fraction = (float)i / steps;
            if (Contains(ground, feet, delta * fraction)) { previous = fraction; continue; }
            // 충돌 직전까지 접근하되 물 셀에는 발을 넣지 않습니다.
            float low = previous, high = fraction;
            for (int search = 0; search < 10; search++)
            {
                float mid = (low + high) * .5f;
                if (Contains(ground, feet, delta * mid)) low = mid;
                else high = mid;
            }
            return delta * low;
        }
        return delta;
    }

    private static bool Contains(Tilemap ground, Bounds feet, Vector2 delta)
    {
        feet.center += (Vector3)delta;
        return PlayerTurretPlacementRules.IsOnGround(ground, feet);
    }
}

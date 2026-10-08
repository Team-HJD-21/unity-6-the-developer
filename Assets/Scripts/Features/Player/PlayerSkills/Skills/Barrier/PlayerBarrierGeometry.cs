using UnityEngine;

/// <summary>방벽의 폭과 적 반경을 고려한 선분 교차 판정입니다.</summary>
public static class PlayerBarrierGeometry
{
    public static bool Intersects(Transform transform, Vector2 start, Vector2 end, float width, Vector2 origin, Vector2 goal, out float fraction)
    {
        // 적의 발밑 반경을 고려해 모서리를 비스듬히 통과하는 것을 방지합니다.
        Vector2 a = transform.InverseTransformPoint(origin);
        Vector2 b = transform.InverseTransformPoint(goal);
        Vector2 delta = b - a;
        var half = new Vector2(Vector2.Distance(start, end) * .5f + .15f, width * .5f + .15f);
        float low = 0, high = 1;
        for (int axis = 0; axis < 2; axis++)
        {
            if (Mathf.Abs(delta[axis]) < .00001f)
            {
                if (Mathf.Abs(a[axis]) > half[axis]) { fraction = 0; return false; }
                continue;
            }
            float enter = (-half[axis] - a[axis]) / delta[axis];
            float leave = (half[axis] - a[axis]) / delta[axis];
            if (enter > leave) { float swap = enter; enter = leave; leave = swap; }
            low = Mathf.Max(low, enter);
            high = Mathf.Min(high, leave);
            if (low > high) { fraction = 0; return false; }
        }
        fraction = low;
        return true;
    }
}

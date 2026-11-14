using UnityEngine;

/// <summary>전장 좌표와 비율을 보존한 전체도 좌표의 변환입니다. 점유/명령 판정에 사용하지 않습니다.</summary>
public static class TacticalMapMath
{
    public static bool TryBounds(Vector2 leftTop, Vector2 rightBottom, out Rect bounds)
    {
        bounds = default;
        if (!float.IsFinite(leftTop.x) || !float.IsFinite(leftTop.y) || !float.IsFinite(rightBottom.x) || !float.IsFinite(rightBottom.y) ||
            rightBottom.x - leftTop.x <= .01f || leftTop.y - rightBottom.y <= .01f) return false;
        bounds = Rect.MinMaxRect(leftTop.x, rightBottom.y, rightBottom.x, leftTop.y); return true;
    }

    public static float SmoothProgress(float elapsed, float duration)
        => !float.IsFinite(duration) || duration <= .0001f ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));

    /// <summary>지역 texture를 Scene 카메라 bounds에 늘이지 않고 실제 world 교차 구간만 배치합니다.</summary>
    public static bool TryRasterPlacement(Rect source, Rect world, Rect area, out Rect placement, out Rect uv)
    {
        placement = uv = default;
        if (source.width <= 0 || source.height <= 0 || world.width <= 0 || world.height <= 0) return false;
        var clipped = Rect.MinMaxRect(Mathf.Max(source.xMin, world.xMin), Mathf.Max(source.yMin, world.yMin),
            Mathf.Min(source.xMax, world.xMax), Mathf.Min(source.yMax, world.yMax));
        if (clipped.width <= 0 || clipped.height <= 0) return false;
        Vector2 a = Project(clipped.min, world, area), b = Project(clipped.max, world, area);
        placement = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        uv = new Rect((clipped.xMin - source.xMin) / source.width, (clipped.yMin - source.yMin) / source.height,
            clipped.width / source.width, clipped.height / source.height);
        return true;
    }
    public readonly struct CameraFrame
    {
        public readonly float Size, Aspect;
        public readonly Rect Viewport;
        public CameraFrame(float size, float aspect, Rect viewport) { Size = size; Aspect = aspect; Viewport = viewport; }
    }
    /// <summary>맵 전체를 볼 수 있는 축소 한계입니다. 넘치는 화면 영역은 뷰포트 바깥 여백으로 분리합니다.</summary>
    public static float ClampViewSize(float requested, Rect world, float aspect, float minimum)
    {
        aspect = float.IsFinite(aspect) && aspect > 0 ? aspect : 1;
        float limit = Mathf.Max(.01f, Mathf.Max(world.height * .5f, world.width / (2 * aspect)));
        minimum = float.IsFinite(minimum) ? Mathf.Clamp(minimum, .01f, limit) : .01f;
        return Mathf.Clamp(float.IsFinite(requested) ? requested : minimum, minimum, limit);
    }
    public static CameraFrame FrameCamera(Rect world, float zoom, float fullAspect, Rect fullViewport)
    {
        float w = Mathf.Min(1, world.width / (2 * zoom * fullAspect));
        float h = Mathf.Min(1, world.height / (2 * zoom));
        var viewport = new Rect(fullViewport.center - Vector2.Scale(fullViewport.size, new Vector2(w,h)) * .5f,
            Vector2.Scale(fullViewport.size, new Vector2(w,h)));
        return new CameraFrame(zoom * h, fullAspect * w / h, viewport);
    }
    public static Vector2 ClampViewCenter(Vector2 requested, Rect world, float size, float aspect)
    {
        if (!float.IsFinite(requested.x) || !float.IsFinite(requested.y)) requested = world.center;
        Vector2 half = new(size * aspect, size);
        return new Vector2(world.width <= half.x * 2 ? world.center.x : Mathf.Clamp(requested.x, world.xMin + half.x, world.xMax - half.x),
            world.height <= half.y * 2 ? world.center.y : Mathf.Clamp(requested.y, world.yMin + half.y, world.yMax - half.y));
    }
    public static Rect Fit(Rect area, Rect world)
    {
        float scale = Mathf.Min(area.width / Mathf.Max(.01f, world.width), area.height / Mathf.Max(.01f, world.height));
        var size = world.size * scale;
        return new Rect(area.center - size * .5f, size);
    }
    public static Vector2 Project(Vector2 position, Rect world, Rect area)
    {
        Rect fit = Fit(area, world);
        return fit.min + Vector2.Scale((position - world.min), new Vector2(fit.width / world.width, fit.height / world.height));
    }
    public static Vector2 Unproject(Vector2 point, Rect world, Rect area)
    {
        Rect fit = Fit(area, world);
        Vector2 uv = new(Mathf.Clamp01((point.x - fit.xMin) / fit.width), Mathf.Clamp01((point.y - fit.yMin) / fit.height));
        return world.min + Vector2.Scale(uv, world.size);
    }
    public static Rect CameraRect(Vector2 center, float size, float aspect) => new(center - new Vector2(size * aspect, size), new Vector2(size * aspect * 2, size * 2));
    public static float FitAround(Vector2 center, Rect contents, float aspect, float minimum)
        => Mathf.Max(minimum, Mathf.Max(Mathf.Abs(contents.yMin - center.y), Mathf.Abs(contents.yMax - center.y)),
            Mathf.Max(Mathf.Abs(contents.xMin - center.x), Mathf.Abs(contents.xMax - center.x)) / Mathf.Max(.1f, aspect));
}

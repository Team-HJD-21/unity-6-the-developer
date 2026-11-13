using UnityEngine;
using UnityEngine.UI;

/// <summary>레이캐스트하지 않는 별도 로컬 커서 Canvas입니다. HUD 마스크와 배율에 종속되지 않습니다.</summary>
public sealed class CombatCursorView : MonoBehaviour
{
    public RectTransform Reticle;
    public RawImage Art;
    public Color IdleColor = new(.933f, .918f, .859f, 1);
    public Color ShotColor = new(.839f, .749f, .486f, 1);
    [Min(.01f)] public float PulseSeconds = .16f;
    private float _shotAt = float.NegativeInfinity;
    public float Pulse { get; private set; }
    public void Shot() => _shotAt = Time.unscaledTime;
    public void Present(Vector2 screen, bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible) { _shotAt = float.NegativeInfinity; Pulse = 0; return; }
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out var local);
        Reticle.anchoredPosition = local;
        Pulse = 1 - Mathf.SmoothStep(0, 1, (Time.unscaledTime - _shotAt) / PulseSeconds);
        Reticle.localScale = Vector3.one * (1 + .22f * Pulse);
        Art.color = Color.Lerp(IdleColor, ShotColor, Pulse);
    }
}

using UnityEngine;

/// <summary>방벽·비콘의 스프라이트와 발광 표현만 관리합니다.</summary>
public sealed class PlayerFieldDeviceVisual
{
    private readonly Transform _root;
    private readonly SpriteRenderer _firstEmitter, _secondEmitter;
    private readonly LineRenderer _membrane;
    private readonly float _visualScale;
    private Material _material;
    private LineRenderer _energy;
    public float VisualScale => _visualScale;
    public PlayerFieldDeviceVisual(Transform root, SpriteRenderer first, SpriteRenderer second, LineRenderer membrane, float scale)
    { _root = root; _firstEmitter = first; _secondEmitter = second; _membrane = membrane; _visualScale = Mathf.Max(.1f, scale); }
    public void Initialize(PlayerFieldDeviceKind kind, Vector3 start, Vector3 end, float width, float radius)
    {
        _material = new Material(Shader.Find("Sprites/Default"));
        var color = kind == PlayerFieldDeviceKind.Barrier ? new Color(.15f, .95f, 1) : new Color(1, .65f, .15f);
        if (kind == PlayerFieldDeviceKind.Barrier)
        {
            _energy = _membrane != null ? _membrane : Line("Energy Membrane", _material, width + .18f, color);
            _energy.sharedMaterial = _material;
            _energy.useWorldSpace = true;
            _energy.startWidth = _energy.endWidth = width + .18f;
            _energy.startColor = _energy.endColor = new Color(color.r, color.g, color.b, .5f);
            PlayerFieldLineFactory.SetLine(_energy, start + Vector3.up * .3f, end + Vector3.up * .3f);
            PlayerFieldLineFactory.SetLine(Line("Ground Edge", _material, .06f, Color.white), start, end);
            Pulse("First Pulse", start, color);
            Pulse("Second Pulse", end, color);
        }
        else
        {
            Pulse("Beacon Emitter", start, color);
            _energy = Line("Attraction Ring", _material, .035f, new Color(1, .65f, .15f, .35f));
            PlayerFieldLineFactory.Circle(_energy, start, radius);
        }
        PlaceEmitter(_firstEmitter, start);
        PlaceEmitter(_secondEmitter, end);
    }
    public void Refresh(PlayerFieldDeviceKind kind, float healthRatio, bool flashing, float radius)
    {
        if (kind == PlayerFieldDeviceKind.Barrier)
        {
            float ratio = healthRatio;
            var color = flashing ? Color.white : Color.Lerp(new Color(1, .3f, .2f), new Color(.15f, .95f, 1), ratio);
            color.a = .45f + .15f * Mathf.Sin(Time.time * (ratio < .3f ? 20 : 4));
            _energy.startColor = _energy.endColor = color;
        }
        else PlayerFieldLineFactory.Circle(_energy, _root.position, radius * (.9f + .1f * Mathf.Sin(Time.time * 3)));
        Color bodyColor = flashing ? new Color(1, .6f, .6f) : Color.white;
        if (_firstEmitter != null) _firstEmitter.color = bodyColor;
        if (_secondEmitter != null) _secondEmitter.color = bodyColor;
    }

    private static void PlaceEmitter(SpriteRenderer emitter, Vector3 position)
    {
        if (emitter == null) return;
        emitter.transform.position = position;
        // 본체는 고정된 탑다운 그림이므로 방벽 각도가 바뀌어도 뒤집지 않습니다.
        emitter.transform.rotation = Quaternion.identity;
    }
    private void Pulse(string name, Vector3 position, Color color)
    {
        var ring = Line(name, _material, .08f, color);
        PlayerFieldLineFactory.Circle(ring, position, .23f * VisualScale, 6);
        PlayerFieldLineFactory.SetLine(Line(name + " Core", _material, .16f * VisualScale, Color.white), position, position + Vector3.up * (.2f * VisualScale));
    }
    private LineRenderer Line(string name, Material material, float width, Color color) => PlayerFieldLineFactory.Line(name, material, width, color, _root);
    public void Dispose() => PlayerFieldDevice.Dispose(_material);
}

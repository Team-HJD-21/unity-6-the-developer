using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>설치 성공 때만 제작된 표시용 스프라이트를 움직입니다. 원본 Transform과 충돌은 유지합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerPlacementPresentation : MonoBehaviour
{
    public enum Motion { Land, Deploy, Rise, Bounce }

    [System.Serializable]
    private sealed class Piece
    {
        public SpriteRenderer Source;
        public SpriteRenderer Display;
        public Motion Motion;
        [Min(0)] public float Lift = .45f;
        [Range(0, .5f)] public float Delay;
        public bool BottomPivot = true;
        [System.NonSerialized] public bool WasHidden;
    }

    [SerializeField, Min(.05f)] private float _duration = .32f;
    [SerializeField] private Piece[] _pieces;
    [SerializeField] private bool _revealEnergyLines;
    private LineRenderer[] _lines;
    private float[] _lineWidths;
    private float _elapsed;
    public bool IsPlaying { get; private set; }
    public float Progress => IsPlaying ? Mathf.Clamp01(_elapsed / _duration) : 1;

    /// <summary>본체 초기화와 Scene/Match 시간 연결 후 호출합니다. 체크포인트 복원 때는 재생하지 않습니다.</summary>
    public void Play()
    {
        Finish();
        if (!isActiveAndEnabled || _pieces == null || _pieces.Length == 0) return;
        foreach (var piece in _pieces)
        {
            if (piece.Source == null || piece.Display == null) continue;
            piece.WasHidden = piece.Source.forceRenderingOff;
            piece.Source.forceRenderingOff = true;
        }
        if (_revealEnergyLines)
        {
            _lines = GetComponentsInChildren<LineRenderer>();
            _lineWidths = new float[_lines.Length];
            for (int i = 0; i < _lines.Length; i++) _lineWidths[i] = _lines[i].widthMultiplier;
        }
        _elapsed = 0;
        IsPlaying = true;
        ApplyPose();
    }

    private void LateUpdate() => Tick(FieldSimulationTime.DeltaTime(this));

    public void Tick(float elapsed)
    {
        if (!IsPlaying || !FieldSimulationTime.CanAdvance(this)) return;
        if (!float.IsFinite(elapsed) || elapsed < 0) return;
        _elapsed += elapsed;
        if (_elapsed >= _duration) { Finish(); return; }
        ApplyPose();
    }

    private void ApplyPose()
    {
        foreach (var piece in _pieces)
        {
            if (piece.Source == null || piece.Display == null) continue;
            float t = Mathf.Clamp01((Progress - piece.Delay) / (1 - piece.Delay));
            float fall = Mathf.Clamp01(t / .58f);
            float landed = Mathf.Clamp01((t - .58f) / .42f);
            float settle = Mathf.Sin(landed * Mathf.PI);
            float height = piece.Lift * (1 - fall * fall);
            var scale = new Vector3(1 + .045f * settle, 1 - .055f * settle, 1);
            switch (piece.Motion)
            {
                case Motion.Deploy:
                    float unfold = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, .94f, t));
                    scale = new Vector3(Mathf.Lerp(.82f, 1, unfold), Mathf.Lerp(.62f, 1, unfold), 1);
                    break;
                case Motion.Rise:
                    height = 0;
                    scale = new Vector3(Mathf.Lerp(.94f, 1, t), Mathf.Lerp(.08f, 1, Mathf.SmoothStep(0, 1, t)), 1);
                    break;
                case Motion.Bounce:
                    height += piece.Lift * .22f * settle * (1 - landed);
                    break;
            }
            var source = piece.Source;
            var display = piece.Display;
            display.sprite = source.sprite;
            display.sharedMaterial = source.sharedMaterial;
            display.sortingLayerID = source.sortingLayerID;
            display.sortingOrder = source.sortingOrder;
            display.flipX = source.flipX; display.flipY = source.flipY;
            display.enabled = source.enabled && !piece.WasHidden;
            var color = source.color;
            color.a *= Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .16f));
            display.color = color;
            var pivot = piece.BottomPivot && source.sprite != null ? new Vector3(0, source.sprite.bounds.min.y, 0) : Vector3.zero;
            display.transform.localPosition = pivot - Vector3.Scale(pivot, scale) + source.transform.InverseTransformVector(Vector3.up * height);
            display.transform.localRotation = Quaternion.identity;
            display.transform.localScale = scale;
        }
        if (_lines != null)
        {
            float reveal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1, Progress));
            for (int i = 0; i < _lines.Length; i++)
                if (_lines[i] != null) _lines[i].widthMultiplier = _lineWidths[i] * reveal;
        }
    }

    public void Finish()
    {
        if (_pieces != null) foreach (var piece in _pieces)
        {
            if (IsPlaying && piece.Source != null) piece.Source.forceRenderingOff = piece.WasHidden;
            if (piece.Display == null) continue;
            piece.Display.enabled = false;
            piece.Display.transform.localPosition = Vector3.zero;
            piece.Display.transform.localRotation = Quaternion.identity;
            piece.Display.transform.localScale = Vector3.one;
        }
        if (_lines != null) for (int i = 0; i < _lines.Length; i++)
            if (_lines[i] != null) _lines[i].widthMultiplier = _lineWidths[i];
        _lines = null; _lineWidths = null;
        IsPlaying = false;
    }

    private void OnDisable() => Finish();
}

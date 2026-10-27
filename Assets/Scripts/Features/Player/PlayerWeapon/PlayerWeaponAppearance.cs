using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>기존 몸체 프레임에 맞는 별도 무기 그림을 연결합니다. 전투 수치는 소유하지 않습니다.</summary>
[CreateAssetMenu(menuName = "Player/Weapon Appearance")]
public sealed class PlayerWeaponAppearance : ScriptableObject
{
    [Serializable]
    public sealed class Frame
    {
        [SerializeField] private Sprite _body;
        [SerializeField] private Sprite _weapon;
        [SerializeField] private Vector2 _localPosition;
        [SerializeField] private float _localAngle;
        [SerializeField] private int _sortingOffset = 1;
        [SerializeField] private bool _overrideMuzzle;
        [SerializeField] private Vector2 _muzzleLocalPosition;
        [SerializeField] private Vector2 _shotMuzzleLocalPosition;

        public Sprite Body => _body;
        public Sprite Weapon => _weapon;
        public Vector2 LocalPosition => _localPosition;
        public float LocalAngle => _localAngle;
        public int SortingOffset => _sortingOffset;
        public bool OverrideMuzzle => _overrideMuzzle;
        public Vector2 MuzzleLocalPosition => _muzzleLocalPosition;
        public Vector2 ShotMuzzleLocalPosition => _shotMuzzleLocalPosition;
    }

    [SerializeField] private Frame[] _frames = Array.Empty<Frame>();
    private Dictionary<Sprite, Frame> _framesByBody;
    private bool _configurationValid;

    public bool IsConfigured
    {
        get
        {
            if (_framesByBody == null) BuildLookup();
            return _configurationValid;
        }
    }
    public int FrameCount => _frames?.Length ?? 0;

    public bool TryGetFrame(Sprite body, out Frame frame)
    {
        frame = null;
        if (body == null || !IsConfigured) return false;
        return _framesByBody.TryGetValue(body, out frame) && frame.Weapon != null;
    }

    private void BuildLookup()
    {
        _framesByBody = new Dictionary<Sprite, Frame>();
        _configurationValid = _frames != null && _frames.Length > 0;
        if (!_configurationValid) return;
        foreach (Frame frame in _frames)
        {
            if (frame == null || frame.Body == null || frame.Weapon == null ||
                !IsFinite(frame.LocalPosition.x) || !IsFinite(frame.LocalPosition.y) || !IsFinite(frame.LocalAngle) ||
                (frame.OverrideMuzzle && (!IsFinite(frame.MuzzleLocalPosition.x) || !IsFinite(frame.MuzzleLocalPosition.y) ||
                    !IsFinite(frame.ShotMuzzleLocalPosition.x) || !IsFinite(frame.ShotMuzzleLocalPosition.y))))
            {
                _configurationValid = false;
                continue;
            }
            if (!_framesByBody.TryAdd(frame.Body, frame))
            {
                _configurationValid = false;
                Debug.LogWarning($"[{name}] Duplicate body frame: {frame.Body.name}", this);
            }
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void OnEnable() => _framesByBody = null;
    private void OnValidate() => _framesByBody = null;
}

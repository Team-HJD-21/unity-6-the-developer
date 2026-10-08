using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBarrierSkill : MonoBehaviour
{
    [Header("Barrier")]
    [SerializeField] private PlayerFieldDevice _prefab;
    [SerializeField, Min(1)] private int _maxCharges = 3;
    [SerializeField, Min(.1f)] private float _rechargeSeconds = 18;
    [SerializeField, Min(1)] private float _health = 300;
    [SerializeField, Min(.1f)] private float _duration = 8;
    [Header("Two-pulse placement")]
    [SerializeField, Min(.1f)] private float _minDistance = 1;
    [SerializeField, Min(.1f)] private float _maxDistance = 6;
    [SerializeField, Min(.1f)] private float _width = .3f;
    [SerializeField, Min(.1f)] private float _pendingSeconds = 15;
    [SerializeField] private LayerMask _terrainLayers = ~0;
    private PlayerSkillController _skills;
    private readonly List<PlayerFieldDevice> _deployed = new List<PlayerFieldDevice>();
    private int _charges;
    private float _rechargeRemaining, _pendingRemaining;
    private Vector3 _first;
    private GameObject _preview;
    private Material _previewMaterial;
    private LineRenderer _previewLine, _firstPulse, _secondPulse;
    private SpriteRenderer _secondEmitterPreview;
    public bool HasPrefab => _prefab != null;
    public int Charges => _charges;
    public float RechargeSeconds => Mathf.Max(.1f, _rechargeSeconds);
    public float RechargeRemaining => _charges < _maxCharges ? _rechargeRemaining : 0;
    public bool IsPending => _preview != null;
    public string PlacementHint
    {
        get
        {
            if (!IsPending) return "";
            ValidateConnection(_first, transform.position, out string reason);
            return reason.Length > 0 ? reason : "같은 스킬 키로 두 번째 펄스 설치 · 마우스 오른쪽 버튼으로 취소";
        }
    }

    private void Awake()
    {
        _skills = GetComponent<PlayerSkillController>();
        _maxCharges = Mathf.Max(1, _maxCharges);
        _width = Mathf.Max(.1f, _width);
        _charges = _maxCharges;
    }

    private void Update()
    {
        Tick(Time.deltaTime);
        if (!IsPending) return;
        if (_skills == null || !_skills.Loadout.Contains(PlayerSkillId.EnergyBarrier)) { CancelPlacement(); return; }
        if (GameInput.SecondaryPointerWasPressedThisFrame) { CancelPlacement(); return; }
        bool valid = ValidateConnection(_first, transform.position, out _);
        Color color = valid ? new Color(.15f, .95f, 1, .65f) : new Color(1, .25f, .2f, .65f);
        _previewLine.startColor = _previewLine.endColor = color;
        _secondPulse.startColor = _secondPulse.endColor = color;
        if (_secondEmitterPreview != null)
        {
            _secondEmitterPreview.transform.position = transform.position;
            _secondEmitterPreview.color = new Color(color.r, color.g, color.b, .6f);
        }
        PlayerFieldDevice.SetLine(_previewLine, _first, transform.position);
        PlayerFieldDevice.Circle(_secondPulse, transform.position, .23f, 6);
    }

    public void Tick(float elapsed)
    {
        elapsed = Mathf.Max(0, elapsed);
        if (_charges < _maxCharges)
        {
            _rechargeRemaining -= elapsed;
            while (_rechargeRemaining <= 0 && _charges < _maxCharges)
            {
                _charges++;
                _rechargeRemaining += RechargeSeconds;
            }
            if (_charges >= _maxCharges) _rechargeRemaining = 0;
        }
        if (IsPending)
        {
            _pendingRemaining -= elapsed;
            if (_pendingRemaining <= 0) CancelPlacement();
        }
    }

    public bool TryUseSkill()
    {
        if (!isActiveAndEnabled || !HasPrefab || _charges <= 0 || _skills == null ||
            !_skills.CanUse || !_skills.Loadout.Contains(PlayerSkillId.EnergyBarrier)) return false;
        _deployed.RemoveAll(device => device == null || !device.IsAlive);
        if (_deployed.Count >= _maxCharges) return false;
        if (!IsPending)
        {
            if (!IsClear(transform.position, transform.position)) return false;
            _first = transform.position;
            _pendingRemaining = Mathf.Max(.1f, _pendingSeconds);
            _preview = new GameObject("Barrier Placement Preview");
            _previewMaterial = new Material(Shader.Find("Sprites/Default"));
            _previewLine = PlayerFieldDevice.Line("Connection", _previewMaterial, _width, Color.cyan, _preview.transform);
            _firstPulse = PlayerFieldDevice.Line("First Pulse", _previewMaterial, .07f, Color.cyan, _preview.transform);
            _secondPulse = PlayerFieldDevice.Line("Second Pulse Preview", _previewMaterial, .07f, Color.cyan, _preview.transform);
            if (_prefab.EmitterSprite != null)
            {
                CreateEmitterPreview("First Emitter", _first, Color.white);
                _secondEmitterPreview = CreateEmitterPreview("Second Emitter Preview", _first, new Color(1, 1, 1, .5f));
            }
            PlayerFieldDevice.Circle(_firstPulse, _first, .23f, 6);
            PlayerFieldDevice.Circle(_secondPulse, _first, .23f, 6);
            PlayerFieldDevice.SetLine(_previewLine, _first, _first);
            return true;
        }
        if (!ValidateConnection(_first, transform.position, out _)) return false;
        var barrier = Instantiate(_prefab, (_first + transform.position) * .5f, Quaternion.identity);
        barrier.Initialize(PlayerFieldDeviceKind.Barrier, _first, transform.position, _width, _health, _duration);
        _deployed.Add(barrier);
        if (_charges == _maxCharges) _rechargeRemaining = RechargeSeconds;
        _charges--;
        CancelPlacement();
        return true;
    }

    private SpriteRenderer CreateEmitterPreview(string name, Vector3 position, Color color)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(_preview.transform, false);
        renderer.transform.position = position;
        renderer.sprite = _prefab.EmitterSprite;
        renderer.transform.localScale = _prefab.EmitterScale;
        renderer.color = color;
        renderer.sortingOrder = 110;
        return renderer;
    }

    public bool ValidateConnection(Vector3 start, Vector3 end, out string reason)
    {
        float distance = Vector2.Distance(start, end);
        if (distance < Mathf.Max(.1f, _minDistance)) { reason = "펄스 사이 간격이 너무 짧습니다"; return false; }
        if (distance > Mathf.Max(_minDistance, _maxDistance)) { reason = "연결 거리 초과"; return false; }
        if (!IsClear(start, end)) { reason = "지형에 가로막힘"; return false; }
        reason = "";
        return true;
    }

    private bool IsClear(Vector3 start, Vector3 end)
    {
        Vector2 delta = end - start;
        var size = new Vector2(delta.magnitude + _width, _width + .2f);
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        foreach (var hit in Physics2D.OverlapBoxAll((start + end) * .5f, size, angle, _terrainLayers))
        {
            if (hit == null || hit.isTrigger || hit.GetComponentInParent<PlayerInputHandler>() != null ||
                hit.GetComponentInParent<Monster>() != null || hit.GetComponentInParent<EnemyHealth>() != null) continue;
            int layer = hit.gameObject.layer;
            if ((hit.excludeLayers.value & (1 << gameObject.layer)) != 0) continue;
            if (layer == LayerMask.NameToLayer("Bullet") || layer == LayerMask.NameToLayer("PlayerBulletCollider") ||
                layer == LayerMask.NameToLayer("EmenyBullet")) continue;
            if (Physics2D.GetIgnoreLayerCollision(gameObject.layer, layer)) continue;
            return false;
        }
        return true;
    }

    public void CancelPlacement()
    {
        if (_preview != null) _preview.SetActive(false);
        PlayerFieldDevice.Dispose(_preview);
        PlayerFieldDevice.Dispose(_previewMaterial);
        _preview = null;
        _previewMaterial = null;
        _secondEmitterPreview = null;
    }

    private void OnDisable() => CancelPlacement();
    private void OnDestroy()
    {
        CancelPlacement();
        foreach (var device in _deployed) if (device != null) PlayerFieldDevice.Dispose(device.gameObject);
    }
}

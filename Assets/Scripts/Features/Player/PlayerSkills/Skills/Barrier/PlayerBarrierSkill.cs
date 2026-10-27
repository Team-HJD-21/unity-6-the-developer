using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBarrierSkill : PlayerSkillBase
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
    private readonly List<PlayerFieldDevice> _deployed = new List<PlayerFieldDevice>();
    private PlayerSkillCharges _charges;
    private float _pendingRemaining;
    private Vector3 _first;
    private PlayerBarrierPlacementPreview _preview;
    public override bool HasPrefab => _prefab != null;
    public override PlayerSkillId Id => PlayerSkillId.EnergyBarrier;
    public override int AvailableCount => Charges;
    public override float RemainingCooldown => RechargeRemaining;
    public override float CooldownDuration => RechargeSeconds;
    public override bool IsReady => HasPrefab && Charges > 0;
    public int Charges => _charges != null ? _charges.Current : 0;
    public float RechargeSeconds => Mathf.Max(.1f, _rechargeSeconds);
    public float RechargeRemaining => _charges != null ? _charges.Remaining : 0;
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

    protected override void Awake()
    {
        base.Awake();
        _maxCharges = Mathf.Max(1, _maxCharges);
        _width = Mathf.Max(.1f, _width);
        _charges = new PlayerSkillCharges(_maxCharges, RechargeSeconds);
    }

    private void Update()
    {
        Tick(Time.deltaTime);
        if (!IsPending) return;
        GameInput.ReservePrimaryPointerForPlacement();
        if ((Controller != null && !Controller.Loadout.Contains(PlayerSkillId.EnergyBarrier)) || GameInput.SecondaryPointerWasPressedThisFrame)
        { CancelPlacement(); return; }
        _preview.Refresh(transform.position, ValidateConnection(_first, transform.position, out _));
    }

    public void Tick(float elapsed)
    {
        if (elapsed <= 0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
        _charges?.Tick(elapsed);
        if (!IsPending) return;
        _pendingRemaining -= elapsed;
        if (_pendingRemaining <= 0) CancelPlacement();
    }

    protected override bool TryActivate()
    {
        GameInput.ReservePrimaryPointerForPlacement();
        _deployed.RemoveAll(device => device == null || !device.IsAlive);
        if (_deployed.Count >= _maxCharges) return false;
        if (!IsPending)
        {
            if (!IsClear(transform.position, transform.position)) return false;
            _first = transform.position;
            _pendingRemaining = Mathf.Max(.1f, _pendingSeconds);
            _preview = new PlayerBarrierPlacementPreview(_prefab, _first, _width);
            return true;
        }
        if (!ValidateConnection(_first, transform.position, out _)) return false;
        var barrier = Instantiate(_prefab, (_first + transform.position) * .5f, Quaternion.identity);
        barrier.Initialize(PlayerFieldDeviceKind.Barrier, _first, transform.position, _width, _health, _duration, owner: transform);
        _deployed.Add(barrier);
        _charges.TryConsume();
        CancelPlacement();
        return true;
    }



    public bool ValidateConnection(Vector3 start, Vector3 end, out string reason)
    {
        if (!IsFinite(start) || !IsFinite(end)) { reason = "유효하지 않은 설치 위치"; return false; }
        float distance = Vector2.Distance(start, end);
        if (distance < Mathf.Max(.1f, _minDistance)) { reason = "펄스 사이 간격이 너무 짧습니다"; return false; }
        if (distance > Mathf.Max(_minDistance, _maxDistance)) { reason = "연결 거리 초과"; return false; }
        if (!IsClear(start, end)) { reason = "지형에 가로막힘"; return false; }
        reason = "";
        return true;
    }

    private bool IsClear(Vector3 start, Vector3 end) => PlayerBarrierPlacementRules.IsClear(gameObject, _terrainLayers, _width, start, end);

    private static bool IsFinite(Vector3 point) =>
        !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y);

    public void CancelPlacement() { _preview?.Dispose(); _preview = null; }

    private void OnDisable() => CancelPlacement();
    private void OnDestroy()
    {
        CancelPlacement();
        foreach (var device in _deployed) if (device != null) PlayerFieldDevice.Dispose(device.gameObject);
    }
}

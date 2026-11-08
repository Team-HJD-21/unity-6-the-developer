// 방벽의 첫 점·배치 중 의도와 실제 생성된 방벽을 관리합니다.
using TeamHJD.Game.Presentation.Scene;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class PlayerBarrierSkill : PlayerPlacementSkillBase
{
    [Header("Barrier")]
    [SerializeField] private PlayerFieldDevice _prefab;
    [SerializeField, Min(1)] private int _maxCharges = 3;
    [SerializeField, Min(.1f)] private float _rechargeSeconds = 18;
    [SerializeField, Min(1)] private float _health = 300;
    [SerializeField, Min(.1f)] private float _duration = 8;
    [Header("Two-click placement")]
    [SerializeField, Min(.1f)] private float _minDistance = 1;
    [SerializeField, Min(.1f)] private float _maxDistance = 13.5f;
    [SerializeField, Min(.1f)] private float _width = .3f;
    [SerializeField] private LayerMask _terrainLayers = ~0;
    private readonly List<PlayerFieldDevice> _deployed = new();
    private PlayerSkillCharges _charges;
    private bool _hasFirst;
    private Vector3 _first;
    public override bool HasPrefab => _prefab != null && HasPlacementPreview;
    public override PlayerSkillId Id => PlayerSkillId.EnergyBarrier;
    public override int AvailableCount => Charges;
    public override float RemainingCooldown => RechargeRemaining;
    public override float CooldownDuration => RechargeSeconds;
    public override bool IsReady => HasPrefab && Charges > 0;
    public int Charges => _charges != null ? _charges.Current : 0;
    public float RechargeSeconds => Mathf.Max(.1f, _rechargeSeconds);
    public float RechargeRemaining => _charges != null ? _charges.Remaining : 0;
    public bool IsPending => IsPlacing;
    public bool HasFirstEmitter => _hasFirst;
    public Vector3 FirstEmitterPosition => _first;
    public override Vector3 PlacementCenter => _hasFirst ? _first : base.PlacementCenter;
    public override float PlacementRadius => _hasFirst ? Mathf.Max(_minDistance, _maxDistance) : base.PlacementRadius;
    protected override Vector3? PlacementAnchor => _hasFirst ? _first : null;
    public override string PlacementHint => !IsPlacing ? "" : _hasFirst
        ? "두 번째 본체를 왼쪽 클릭으로 설치 · 오른쪽 클릭 / Esc / 같은 스킬 키로 취소"
        : "첫 번째 본체를 왼쪽 클릭으로 설치 · 오른쪽 클릭 / Esc / 같은 스킬 키로 취소";
    protected override void Awake()
    {
        base.Awake();
        _maxCharges = Mathf.Max(1, _maxCharges);
        _width = Mathf.Max(.1f, _width);
        _charges = new PlayerSkillCharges(_maxCharges, RechargeSeconds);
    }
    private void Update() => Tick(FieldSimulationTime.DeltaTime(this));
    public void Tick(float elapsed)
    {
        if (!FieldSimulationTime.CanAdvance(this)) return;
        if (elapsed <= 0 || !float.IsFinite(elapsed)) return;
        _charges?.Tick(elapsed);
    }
    protected override string GetPlacementFailure(Vector3 position)
    {
        _deployed.RemoveAll(device => device == null || !device.IsAlive);
        if (_deployed.Count >= _maxCharges) return "방벽을 더 설치할 수 없습니다.";
        if (_hasFirst) return ValidateConnection(_first, position, out string reason) ? "" : reason;
        return CheckEmitter(position, 0);
    }
    private string CheckEmitter(Vector3 position, int index)
    {
        if (!PlayerSkillPlacementUtility.TryGetDeviceFootprint(_prefab, position, index, out Bounds footprint))
            return "방벽 본체 설정을 확인해 주세요.";
        return PlayerSkillDescriptions.TurretPlacementFailure(EvaluateFootprint(footprint));
    }
    protected override bool TryPlace(Vector3 position, out bool complete)
    {
        complete = false;
        if (!_hasFirst)
        {
            _first = position;
            _hasFirst = true;
            return true;
        }
        // 실패한 두 번째 지점은 첫 위치와 미리보기를 유지합니다. 완성에만 수량을 소모합니다.
        if (!_charges.TryConsume()) return false;
        var barrier = Instantiate(_prefab, (_first + position) * .5f, Quaternion.identity);
        MoveToOwnerScene(barrier.gameObject);
        barrier.Initialize(PlayerFieldDeviceKind.Barrier, _first, position, _width, _health, _duration, owner: transform);
        _deployed.Add(barrier);
        complete = true;
        return true;
    }
    public bool ValidateConnection(Vector3 start, Vector3 end, out string reason)
    {
        if (!PlayerSkillPlacementUtility.IsFinite(start) || !PlayerSkillPlacementUtility.IsFinite(end))
        { reason = "유효하지 않은 설치 위치입니다."; return false; }
        float distance = Vector2.Distance(start, end);
        if (distance < Mathf.Max(.1f, _minDistance)) { reason = "본체 사이 간격이 너무 짧습니다."; return false; }
        if (distance > Mathf.Max(_minDistance, _maxDistance)) { reason = "방벽 연결 거리를 초과했습니다."; return false; }
        reason = CheckEmitter(start, 0);
        if (reason.Length > 0) return false;
        reason = CheckEmitter(end, 1);
        if (reason.Length > 0) return false;
        // 연결 구간도 지면 위에 있어야 합니다. 대각선의 빈 모서리를 과하게 막지 않도록 폭 단위로 확인합니다.
        int steps = Mathf.CeilToInt(distance / Mathf.Max(.1f, _width));
        for (int i = 0; i <= steps; i++)
            if (!PlayerTurretPlacementRules.IsOnGround(PlacementGround,
                new Bounds(Vector3.Lerp(start, end, i / (float)steps), new Vector3(_width, _width, 0))))
            { reason = "지면이 부족해 방벽을 연결할 수 없습니다."; return false; }
        if (!PlayerBarrierPlacementRules.IsClear(gameObject, _terrainLayers, _width, start, end))
        { reason = "장애물이나 캐릭터에 가로막혀 방벽을 연결할 수 없습니다."; return false; }
        reason = "";
        return true;
    }
    protected override void OnPlacementCancelled() { _hasFirst = false; _first = default; }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (var device in _deployed) if (device != null) PlayerFieldDevice.Dispose(device.gameObject);
    }
}

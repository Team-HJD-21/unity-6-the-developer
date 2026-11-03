using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBeaconSkill : PlayerPlacementSkillBase
{
    [SerializeField] private PlayerFieldDevice _prefab;
    [SerializeField, Min(.1f)] private float _cooldown = 14;
    [SerializeField, Min(.1f)] private float _duration = 8;
    [SerializeField, Min(.1f)] private float _radius = 12;
    [SerializeField, Min(1)] private float _health = 80;
    private PlayerFieldDevice _active;
    private float _remainingCooldown;
    public override PlayerSkillId Id => PlayerSkillId.LureBeacon;
    public override bool HasPrefab => _prefab != null && HasPlacementPreview;
    public override int AvailableCount => CanDeploy ? 1 : 0;
    public float Cooldown => Mathf.Max(.1f, _cooldown);
    public override float CooldownDuration => Cooldown;
    public override float RemainingCooldown => _remainingCooldown;
    public bool CanDeploy => _remainingCooldown <= 0;
    private void Update() => Tick(Time.deltaTime);
    public void Tick(float elapsed)
    {
        if (elapsed <= 0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
        _remainingCooldown = Mathf.Max(0, _remainingCooldown - elapsed);
    }
    protected override string GetPlacementFailure(Vector3 position)
    {
        if (!PlayerSkillPlacementUtility.TryGetDeviceFootprint(_prefab, position, 0, out Bounds footprint))
            return "비콘 본체 설정을 확인해 주세요.";
        return PlayerSkillDescriptions.TurretPlacementFailure(EvaluateFootprint(footprint));
    }

    protected override bool TryPlace(Vector3 position, out bool complete)
    {
        complete = true;
        if (_active != null) PlayerFieldDevice.Dispose(_active.gameObject);
        _active = Instantiate(_prefab, position, Quaternion.identity);
        MoveToOwnerScene(_active.gameObject);
        _active.Initialize(PlayerFieldDeviceKind.Beacon, position, position, .3f, _health, _duration, _radius, transform);
        _remainingCooldown = Cooldown;
        return true;
    }
    protected override void OnDestroy() { base.OnDestroy(); if (_active != null) PlayerFieldDevice.Dispose(_active.gameObject); }
}

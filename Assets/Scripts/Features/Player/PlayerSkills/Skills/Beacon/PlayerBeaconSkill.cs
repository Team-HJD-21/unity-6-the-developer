using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class PlayerBeaconSkill : PlayerSkillBase
{
    [SerializeField] private PlayerFieldDevice _prefab;
    [SerializeField, Min(.1f)] private float _cooldown = 14;
    [SerializeField, Min(.1f)] private float _duration = 8;
    [SerializeField, Min(.1f)] private float _radius = 12;
    [SerializeField, Min(1)] private float _health = 80;
    private PlayerFieldDevice _active;
    private float _remainingCooldown;
    public override PlayerSkillId Id => PlayerSkillId.LureBeacon;
    public override bool HasPrefab => _prefab != null;
    public override int AvailableCount => CanDeploy ? 1 : 0;
    public float Cooldown => Mathf.Max(.1f, _cooldown);
    public override float CooldownDuration => Cooldown;
    public override float RemainingCooldown => _remainingCooldown;
    public bool CanDeploy => _remainingCooldown <= 0;
    private void Update() => Tick(Time.deltaTime);
    public void Tick(float elapsed)
    {
        if (!PlayerFieldSimulationUtility.CanAdvance(this)) return;
        if (elapsed <= 0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
        _remainingCooldown = Mathf.Max(0, _remainingCooldown - elapsed);
    }
    protected override bool TryActivate()
    {
        if (_active != null) PlayerFieldDevice.Dispose(_active.gameObject);
        _active = Instantiate(_prefab, transform.position, Quaternion.identity);
        PlayerFieldSimulationUtility.BindSpawned(_active.gameObject, this);
        _active.Initialize(PlayerFieldDeviceKind.Beacon, transform.position, transform.position, .3f, _health, _duration, _radius);
        _remainingCooldown = Cooldown;
        return true;
    }
    private void OnDestroy() { if (_active != null) PlayerFieldDevice.Dispose(_active.gameObject); }
}

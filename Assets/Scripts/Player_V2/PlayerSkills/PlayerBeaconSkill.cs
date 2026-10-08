using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBeaconSkill : MonoBehaviour
{
    [SerializeField] private PlayerFieldDevice _prefab;
    [SerializeField, Min(.1f)] private float _cooldown = 14;
    [SerializeField, Min(.1f)] private float _duration = 8;
    [SerializeField, Min(.1f)] private float _radius = 12;
    [SerializeField, Min(1)] private float _health = 80;
    private PlayerSkillController _skills;
    private PlayerFieldDevice _active;
    private float _remainingCooldown;
    public bool HasPrefab => _prefab != null;
    public float Cooldown => Mathf.Max(.1f, _cooldown);
    public float RemainingCooldown => _remainingCooldown;
    public bool CanDeploy => _remainingCooldown <= 0;

    private void Awake() => _skills = GetComponent<PlayerSkillController>();
    private void Update() => Tick(Time.deltaTime);
    public void Tick(float elapsed) => _remainingCooldown = Mathf.Max(0, _remainingCooldown - Mathf.Max(0, elapsed));

    public bool TryUseSkill()
    {
        if (!isActiveAndEnabled || !HasPrefab || !CanDeploy || _skills == null ||
            !_skills.CanUse || !_skills.Loadout.Contains(PlayerSkillId.LureBeacon)) return false;
        if (_active != null) PlayerFieldDevice.Dispose(_active.gameObject);
        _active = Instantiate(_prefab, transform.position, Quaternion.identity);
        _active.Initialize(PlayerFieldDeviceKind.Beacon, transform.position, transform.position, .3f, _health, _duration, _radius);
        _remainingCooldown = Cooldown;
        return true;
    }

    private void OnDestroy() { if (_active != null) PlayerFieldDevice.Dispose(_active.gameObject); }
}

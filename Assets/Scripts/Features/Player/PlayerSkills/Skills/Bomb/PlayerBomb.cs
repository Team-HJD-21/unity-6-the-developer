using UnityEngine;

/// <summary>시한폭탄 설치와 보유 수량 충전을 담당합니다.</summary>
public sealed class PlayerBomb : PlayerSkillBase
{
    [Header("폭탄 기본 설정")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private int maxBombCount = 5;
    [SerializeField] private float bombRechargeTime = 5f;
    [SerializeField] private float bombCooldown = 1f;
    private PlayerSkillCharges _charges;
    private float _remainingCooldown;
    public override PlayerSkillId Id => PlayerSkillId.Bomb;
    public override bool HasPrefab => bombPrefab != null;
    public override int AvailableCount => _charges != null ? _charges.Current : 0;
    public int CurrentBombCount => AvailableCount;
    public int MaxBombCount => Mathf.Max(1, maxBombCount);
    public override float RemainingCooldown => _remainingCooldown;
    public override float CooldownDuration => Mathf.Max(.1f, bombCooldown);
    protected override void Awake()
    {
        base.Awake();
        _charges = new PlayerSkillCharges(MaxBombCount, bombRechargeTime);
    }
    private void Update()
    {
        if (!PlayerFieldSimulationUtility.CanAdvance(this)) return;
        _charges.Tick(Time.deltaTime);
        _remainingCooldown = Mathf.Max(0, _remainingCooldown - Time.deltaTime);
    }
    protected override bool TryActivate()
    {
        if (!_charges.TryConsume()) return false;
        var bomb = Instantiate(bombPrefab, transform.position, Quaternion.identity);
        PlayerFieldSimulationUtility.BindSpawned(bomb, this);
        _remainingCooldown = CooldownDuration;
        return true;
    }
    public void TryDropBomb() => TryUseSkill();
    // 기존 Scene 호출의 호환 진입점입니다. 현재 HUD는 스킬 상태를 직접 읽습니다.
    public void SyncBombUI() { }
}
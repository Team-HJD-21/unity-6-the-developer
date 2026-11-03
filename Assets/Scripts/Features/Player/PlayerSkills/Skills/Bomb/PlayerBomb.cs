using UnityEngine;

/// <summary>시한폭탄 설치와 보유 수량 충전을 담당합니다.</summary>
public sealed class PlayerBomb : PlayerPlacementSkillBase
{
    [Header("폭탄 기본 설정")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private int maxBombCount = 5;
    [SerializeField] private float bombRechargeTime = 5f;
    [SerializeField] private float bombCooldown = 1f;
    private PlayerSkillCharges _charges;
    private float _remainingCooldown;
    private PlayerBombPlacementResult _placementFeedback;
    private float _feedbackUntil;
    public override PlayerSkillId Id => PlayerSkillId.Bomb;
    public override bool HasPrefab => bombPrefab != null && HasPlacementPreview;
    public override int AvailableCount => _charges != null ? _charges.Current : 0;
    public int CurrentBombCount => AvailableCount;
    public int MaxBombCount => Mathf.Max(1, maxBombCount);
    public override float RemainingCooldown => _remainingCooldown;
    public override float CooldownDuration => Mathf.Max(.1f, bombCooldown);
    public PlayerBombPlacementResult PlacementFeedback => IsPlacing || Time.unscaledTime < _feedbackUntil
        ? _placementFeedback : PlayerBombPlacementResult.Allowed;
    protected override void Awake()
    {
        base.Awake();
        _charges = new PlayerSkillCharges(MaxBombCount, bombRechargeTime);
    }
    private void Update()
    {
        _charges.Tick(Time.deltaTime);
        _remainingCooldown = Mathf.Max(0, _remainingCooldown - Time.deltaTime);
    }
    protected override string GetPlacementFailure(Vector3 position)
    {
        _placementFeedback = PlayerBombPlacementRules.Evaluate(bombPrefab, position, gameObject);
        _feedbackUntil = Time.unscaledTime + 2f;
        if (_placementFeedback != PlayerBombPlacementResult.Allowed) return PlayerSkillDescriptions.BombPlacementFailure(_placementFeedback);
        if (!PlayerBombPlacementRules.TryGetFootprint(bombPrefab, position, out Bounds footprint)) return "시한폭탄 본체 설정을 확인해 주세요.";
        var result = EvaluateFootprint(footprint);
        if (result == PlayerTurretPlacementResult.OverlapsPlayer) _placementFeedback = PlayerBombPlacementResult.BlockedByObstacle;
        return PlayerSkillDescriptions.TurretPlacementFailure(result);
    }

    protected override bool TryPlace(Vector3 position, out bool complete)
    {
        complete = true;
        if (!_charges.TryConsume()) return false;
        var bomb = Instantiate(bombPrefab, position, Quaternion.identity);
        MoveToOwnerScene(bomb);
        if (bomb.TryGetComponent(out PlayerBombExplosion explosion)) explosion.BindOwner(transform);
        _remainingCooldown = CooldownDuration;
        _feedbackUntil = 0;
        return true;
    }
    protected override void OnPlacementCancelled() { _feedbackUntil = 0; _placementFeedback = PlayerBombPlacementResult.Allowed; }
    public void TryDropBomb()
    {
        if (Controller != null) Controller.TryUse(Id);
        else TryUseSkill();
    }
    // 기존 Scene 호출의 호환 진입점입니다. 현재 HUD는 스킬 상태를 직접 읽습니다.
    public void SyncBombUI() { }
}

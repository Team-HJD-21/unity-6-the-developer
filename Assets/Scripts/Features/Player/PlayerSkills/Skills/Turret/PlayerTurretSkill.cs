using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>임시 포탑 설치·회수와 손상 비율에 따른 재충전을 담당합니다.</summary>
public sealed class PlayerTurretSkill : PlayerSkillBase
{
    private const float PlacementFeedbackSeconds = 2.5f;

    [Header("Turret Prefab")]
    [SerializeField] private GameObject turretPrefab;
    [Header("Skill Settings")]
    [SerializeField, Min(.1f)] private float skillCooldown = 3f;
    [SerializeField, Min(1)] private int maxTurretCount = 3;
    [SerializeField, Min(.1f)] private float recoveryRange = 2.5f;
    [SerializeField, Min(0)] private float _maximumRepairSeconds = 30;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, -.5f, 0);

    [Header("Placement")]
    [Tooltip("미지정 시 현재 씬에서 Collider가 있는 가장 큰 Tilemap을 사용합니다.")]
    [SerializeField] private Tilemap _placementGround;

    private float _installationRemaining;
    private float _placementFeedbackRemaining;
    private PlayerTurretPlacementResult _placementFeedback;
    private PlayerTurretInventory _inventory;
    private readonly HashSet<TemporaryTurret> _deployed = new();
    public override PlayerSkillId Id => PlayerSkillId.TemporaryTurret;
    public override bool HasPrefab => turretPrefab != null && turretPrefab.GetComponent<TemporaryTurret>() != null;
    public override int AvailableCount => _inventory != null ? _inventory.Available : 0;
    public int CurrentTurretCount => AvailableCount;
    public int MaxTurretCount => Mathf.Max(1, maxTurretCount);
    public float CurrentCooldownTimer => RemainingCooldown;
    public float SkillCooldown => Mathf.Max(.1f, skillCooldown);
    public bool IsCooldown => _installationRemaining > 0;
    public float RepairRemaining => _inventory != null ? _inventory.RepairRemaining : 0;
    public override float RemainingCooldown => AvailableCount > 0 ? _installationRemaining : Mathf.Max(_installationRemaining, RepairRemaining);
    public override float CooldownDuration => AvailableCount > 0 || RepairRemaining <= _installationRemaining ? SkillCooldown : Mathf.Max(.1f, _maximumRepairSeconds);
    public override bool IsReady => HasPrefab && AvailableCount > 0 && _installationRemaining <= 0;
    public TemporaryTurret RecoverableTurret => FindRecoverableTurret();
    public PlayerTurretPlacementResult PlacementFeedback =>
        _placementFeedbackRemaining > 0f ? _placementFeedback : PlayerTurretPlacementResult.Allowed;
    protected override void Awake()
    {
        base.Awake();
        _inventory = new PlayerTurretInventory(MaxTurretCount, _maximumRepairSeconds);
    }
    private void Update() => Tick(Time.deltaTime);
    public void Tick(float elapsed)
    {
        if (elapsed <= 0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
        _installationRemaining = Mathf.Max(0, _installationRemaining - elapsed);
        _placementFeedbackRemaining = Mathf.Max(0, _placementFeedbackRemaining - elapsed);
        _inventory?.Tick(elapsed);
    }
    protected override bool TryActivate()
    {
        Vector3 position = transform.position + spawnOffset;
        if (_placementGround == null)
            _placementGround = PlayerTurretPlacementRules.FindGround(gameObject.scene);

        _placementFeedback = PlayerTurretPlacementRules.Evaluate(turretPrefab, position, gameObject, _placementGround);
        if (_placementFeedback != PlayerTurretPlacementResult.Allowed)
        {
            _placementFeedbackRemaining = PlacementFeedbackSeconds;
            return false;
        }
        _placementFeedbackRemaining = 0f;
        if (!_inventory.TryDeploy()) return false;

        var turret = Instantiate(turretPrefab, position, Quaternion.identity).GetComponent<TemporaryTurret>();
        SceneManager.MoveGameObjectToScene(turret.gameObject, gameObject.scene);
        turret.AssignOwner(this);
        _deployed.Add(turret);
        _installationRemaining = SkillCooldown;
        return true;
    }
    public bool TryRecoverNearest()
    {
        if (!isActiveAndEnabled || (Controller != null && !Controller.CanRecover)) return false;
        var turret = FindRecoverableTurret();
        return turret != null && turret.TryRecover(this);
    }
    private TemporaryTurret FindRecoverableTurret()
    {
        if (!isActiveAndEnabled || (Controller != null && !Controller.CanRecover)) return null;
        float distanceLimit = recoveryRange * recoveryRange;
        TemporaryTurret nearest = null;
        foreach (var turret in _deployed)
        {
            if (turret == null || !turret.isActiveAndEnabled || turret.Health == null || !turret.Health.IsAlive) continue;
            float distance = (turret.transform.position - transform.position).sqrMagnitude;
            if (distance > distanceLimit) continue;
            nearest = turret; distanceLimit = distance;
        }
        return nearest;
    }
    internal bool ReleaseTurret(TemporaryTurret turret, float healthRatio)
    {
        if (!_deployed.Remove(turret)) return false;
        _inventory.Return(healthRatio);
        return true;
    }

    private void OnDisable() => _placementFeedbackRemaining = 0f;

    private void OnDestroy()
    {
        foreach (var turret in _deployed)
        {
            if (turret == null) continue;
            turret.AssignOwner(null);
            PlayerFieldDevice.Dispose(turret.gameObject);
        }
        _deployed.Clear();
    }
}

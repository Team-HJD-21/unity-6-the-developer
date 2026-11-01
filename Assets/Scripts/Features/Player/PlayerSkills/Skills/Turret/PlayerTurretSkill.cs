using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>임시 포탑 설치·회수와 손상 비율에 따른 재충전을 담당합니다.</summary>
public sealed class PlayerTurretSkill : PlayerSkillBase
{
    private const float PreviewCheckInterval = .05f;

    [Header("Turret Prefab")]
    [SerializeField] private GameObject turretPrefab;
    [SerializeField] private PlayerTurretPlacementPreview _previewPrefab;
    [Header("Skill Settings")]
    [SerializeField, Min(.1f)] private float skillCooldown = 3f;
    [SerializeField, Min(1)] private int maxTurretCount = 3;
    [SerializeField, Min(.1f)] private float recoveryRange = 2.5f;
    [SerializeField, Min(0)] private float _maximumRepairSeconds = 30;
    [Header("Placement")]
    [Tooltip("미지정 시 현재 씬에서 Collider가 있는 가장 큰 Tilemap을 사용합니다.")]
    [SerializeField] private Tilemap _placementGround;
    [Tooltip("플레이어 중심의 3×3 격자 간격입니다. 가운데를 제외한 8칸에 설치합니다.")]
    [SerializeField, Min(.1f)] private float _placementCellSize = 4f;

    private float _installationRemaining;
    private PlayerTurretPlacementPreview _preview;
    private PlayerInputHandler _input;
    private float _nextPreviewCheck;
    private int _placementStartFrame;
    private readonly List<RaycastResult> _uiHits = new();
    private PlayerTurretPlacementResult _placementFeedback;
    private PlayerTurretInventory _inventory;
    private readonly HashSet<TemporaryTurret> _deployed = new();
    public override PlayerSkillId Id => PlayerSkillId.TemporaryTurret;
    public override bool HasPrefab => turretPrefab != null && turretPrefab.GetComponent<TemporaryTurret>() != null
        && _previewPrefab != null && _previewPrefab.IsConfigured;
    public bool IsPlacing => _preview != null;
    public string PlacementHint => IsPlacing ? "왼쪽 클릭으로 설치 · 오른쪽 클릭 / Esc로 취소" : "";
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
        IsPlacing ? _placementFeedback : PlayerTurretPlacementResult.Allowed;
    protected override void Awake()
    {
        base.Awake();
        _inventory = new PlayerTurretInventory(MaxTurretCount, _maximumRepairSeconds);
        _input = GetComponent<PlayerInputHandler>();
    }
    private void Update() => Tick(Time.deltaTime);
    public void Tick(float elapsed)
    {
        if (!PlayerFieldSimulationUtility.CanAdvance(this)) return;
        if (elapsed <= 0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
        _installationRemaining = Mathf.Max(0, _installationRemaining - elapsed);
        _inventory?.Tick(elapsed);
    }
    protected override bool TryActivate()
    {
        if (IsPlacing) { CancelPlacement(); return true; }
        _preview = Instantiate(_previewPrefab, transform.position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(_preview.gameObject, gameObject.scene);
        _preview.SetVisible(false);
        _placementStartFrame = Time.frameCount;
        _nextPreviewCheck = 0f;
        return true;
    }

    // UI의 Update가 끝난 뒤 확정하여 같은 클릭이 기존 포탑 선택에도 쓰이지 않게 합니다.
    private void LateUpdate()
    {
        if (!IsPlacing) return;
        if (!CanContinuePlacement() || GameInput.SecondaryPointerWasPressedThisFrame || GameInput.WasPressedThisFrame(GameKey.Escape))
        { CancelPlacement(); return; }
        if (_input == null || !_input.HasAimTarget) { _preview.SetVisible(false); return; }

        Vector3 position = GetPlacementPosition(_input.MouseWorldPosition);
        if (Time.unscaledTime >= _nextPreviewCheck)
        {
            _nextPreviewCheck = Time.unscaledTime + PreviewCheckInterval;
            _placementFeedback = EvaluatePlacement(position);
        }
        _preview.Refresh(position, _placementFeedback == PlayerTurretPlacementResult.Allowed);
        if (Time.frameCount > _placementStartFrame && GameInput.PrimaryPointerWasPressedThisFrame && !IsPointerOverUi())
            TryConfirmPlacement(position);
    }

    private bool CanContinuePlacement() => isActiveAndEnabled && Time.timeScale > 0f && IsReady
        && (ControlState == null || ControlState.CanAct)
        && (Controller == null || Controller.CanUse && Controller.Loadout.Contains(Id));

    private bool IsPointerOverUi()
    {
        if (EventSystem.current == null) return false;
        _uiHits.Clear();
        var pointer = new PointerEventData(EventSystem.current) { position = GameInput.PointerPosition };
        EventSystem.current.RaycastAll(pointer, _uiHits);
        foreach (var hit in _uiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    /// <summary>커서까지의 거리와 관계없이 플레이어 주변 8칸 중 조준 방향의 한 칸을 반환합니다.</summary>
    public Vector3 GetPlacementPosition(Vector2 pointerPosition)
    {
        Vector2 direction = pointerPosition - (Vector2)transform.position;
        if (!PlayerAimUtility.IsValidDirection(direction))
            direction = _input != null ? _input.AimDirection : Vector2.down;
        float angle = Mathf.Atan2(direction.y, direction.x);
        float snappedAngle = Mathf.Round(angle / (Mathf.PI / 4f)) * (Mathf.PI / 4f);
        Vector3 cell = new(Mathf.RoundToInt(Mathf.Cos(snappedAngle)), Mathf.RoundToInt(Mathf.Sin(snappedAngle)), 0f);
        return transform.position + cell * Mathf.Max(.1f, _placementCellSize);
    }

    private PlayerTurretPlacementResult EvaluatePlacement(Vector3 position)
    {
        if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
            float.IsNaN(position.y) || float.IsInfinity(position.y)) return PlayerTurretPlacementResult.InvalidFootprint;
        position.z = transform.position.z;
        Physics2D.SyncTransforms();
        if (_placementGround == null)
            _placementGround = PlayerTurretPlacementRules.FindGround(gameObject.scene);
        return PlayerTurretPlacementRules.Evaluate(turretPrefab, position, gameObject, _placementGround);
    }

    /// <summary>확정 시 다시 검사하고 성공한 경우에만 수량과 쿨타임을 소모합니다.</summary>
    public bool TryConfirmPlacement(Vector3 pointerPosition)
    {
        if (!IsPlacing || !CanContinuePlacement()) return false;
        if (float.IsNaN(pointerPosition.x) || float.IsInfinity(pointerPosition.x) ||
            float.IsNaN(pointerPosition.y) || float.IsInfinity(pointerPosition.y)) return false;
        Vector3 position = GetPlacementPosition(pointerPosition);
        _placementFeedback = EvaluatePlacement(position);
        _preview.Refresh(position, _placementFeedback == PlayerTurretPlacementResult.Allowed);
        if (_placementFeedback != PlayerTurretPlacementResult.Allowed || !_inventory.TryDeploy()) return false;

        var turret = Instantiate(turretPrefab, position, Quaternion.identity).GetComponent<TemporaryTurret>();
        PlayerFieldSimulationUtility.BindSpawned(turret.gameObject, this);
        SceneManager.MoveGameObjectToScene(turret.gameObject, gameObject.scene);
        turret.AssignOwner(this);
        _deployed.Add(turret);
        _installationRemaining = SkillCooldown;
        CancelPlacement();
        return true;
    }

    public void CancelPlacement()
    {
        if (_preview != null)
        {
            _preview.SetVisible(false);
            PlayerFieldDevice.Dispose(_preview.gameObject);
            _preview = null;
        }
        _placementFeedback = PlayerTurretPlacementResult.Allowed;
    }

    public bool TryRecoverNearest()
    {
        if (!isActiveAndEnabled || IsPlacing || (Controller != null && !Controller.CanRecover)) return false;
        var turret = FindRecoverableTurret();
        return turret != null && turret.TryRecover(this);
    }
    private TemporaryTurret FindRecoverableTurret()
    {
        if (!isActiveAndEnabled || IsPlacing || (Controller != null && !Controller.CanRecover)) return null;
        float distanceLimit = recoveryRange * recoveryRange;
        TemporaryTurret nearest = null;
        foreach (var turret in _deployed)
        {
            if (turret == null || !turret.isActiveAndEnabled || turret.Health == null || !turret.Health.IsAlive) continue;
            // 커진 본체 바깥에서도 회수할 수 있도록 중심 대신 표면까지의 XY 거리를 사용합니다.
            float distance = (turret.Health.ApproachPoint(transform.position) - (Vector2)transform.position).sqrMagnitude;
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

    private void OnDisable() => CancelPlacement();

    private void OnDestroy()
    {
        CancelPlacement();
        foreach (var turret in _deployed)
        {
            if (turret == null) continue;
            turret.AssignOwner(null);
            PlayerFieldDevice.Dispose(turret.gameObject);
        }
        _deployed.Clear();
    }
}

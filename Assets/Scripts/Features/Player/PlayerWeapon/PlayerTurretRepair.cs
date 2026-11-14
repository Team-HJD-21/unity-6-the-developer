using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>가까운 필드·플레이어 터렛을 R 키 유지로 수리합니다. 필드 터렛의 파괴 복구도 지원합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler), typeof(PlayerControlState))]
// 입력(-200)·스킬(100)·무기 선택(200) 뒤에 수리 여부를 확정하고,
// PlayerAttack.LateUpdate(300)가 같은 프레임의 공격을 차단하도록 합니다.
[DefaultExecutionOrder(250)]
public sealed class PlayerTurretRepair : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float _repairRange = 3f;
    [Tooltip("초당 HP 회복 또는 파괴 복구 진행 비율. 0.1 = 초당 10%, 파괴 복구는 약 10초")]
    [SerializeField, Range(.01f, 1f)] private float _maximumHealthFractionPerSecond = .1f;
    private PlayerInputHandler _input;
    private PlayerControlState _controls;
    private PlayerSkillController _skills;
    private PlayerSpriteMotion _motion;
    private PlayerDirectionalColliders _colliders;
    private PlayerWeaponRig _rig;
    private PlayerSpannerWeapon _spanner;
    private PlayerMatchPresence _presence;
    private Rigidbody2D _body;
    private PlayerDash _dash;
    private PlayerTurretRepairTarget _heldTarget, _target;
    private bool _restoringHeldTarget;
    private Vector2 _lastPosition;
    private bool _waitingForRelease;
    private bool _moving;

    public FieldTurretBase Target => _target.Field;
    public PlayerTurretHealth PlayerTarget => _target.Player;
    public Transform TargetTransform => _target.Transform;
    public bool TargetNeedsRepair => _target.Exists && _target.CurrentHealth < _target.MaxHealth;
    public bool IsRepairing { get; private set; }
    public bool IsRestoring => _target.Exists && _target.IsDestroyed;
    public float Progress => !_target.Exists ? 0f : IsRestoring
        ? PlayerTurretRestoreProgress.GetProgress(Target)
        : _target.MaxHealth > 0 ? Mathf.Clamp01(_target.CurrentHealth / _target.MaxHealth) : 0f;
    public bool RequiresRelease => _waitingForRelease;
    /// <summary>이번 R 누름을 수리에 사용했으면 놓을 때까지 재장전으로 재사용하지 않습니다.</summary>
    public bool ConsumesRepairInput { get; private set; }
    public bool CanRepair => CanInteract && !_moving && !_waitingForRelease &&
        IsEligible(_target) && (_target.IsDestroyed || _target.CanRepair);
    private Vector2 InteractionPosition => _colliders != null && _colliders.isActiveAndEnabled
        ? _colliders.GroundPosition : (Vector2)transform.position;

    /// <summary>작업 중인 몸체·장비 표시 방향입니다. 마우스 공격 조준 입력은 변경하지 않습니다.</summary>
    public bool TryGetFacingDirection(out Vector2 direction)
    {
        direction = Vector2.zero;
        if (!IsRepairing || !_heldTarget.Exists) return false;
        direction = (Vector2)_heldTarget.Transform.position - InteractionPosition;
        direction = PlayerAimUtility.IsValidDirection(direction) ? direction.normalized : Vector2.down;
        return true;
    }

    // Core는 Player Awake 이후에도 presence를 연결할 수 있습니다.
    private PlayerMatchPresence Presence => _presence != null ? _presence :
        (_presence = GetComponent<PlayerMatchPresence>());
    private bool CanInteract => isActiveAndEnabled && _input != null && _input.isActiveAndEnabled &&
        _controls != null && _controls.CanAct && FieldSimulationTime.CanAdvance(this) &&
        (Presence == null || Presence.CanReport) &&
        (_skills == null || (_skills.CanUse && !_skills.IsPlacingSkill));

    private void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
        _controls = GetComponent<PlayerControlState>();
        _skills = GetComponent<PlayerSkillController>();
        _motion = GetComponent<PlayerSpriteMotion>();
        _colliders = GetComponent<PlayerDirectionalColliders>();
        _rig = GetComponentInChildren<PlayerWeaponRig>(true);
        _presence = GetComponent<PlayerMatchPresence>();
        _body = GetComponent<Rigidbody2D>();
        _dash = GetComponent<PlayerDash>();
    }

    private void OnEnable() => _lastPosition = transform.position;

    private void Update() => Tick(_input != null && _input.IsRepairPressed, FieldSimulationTime.DeltaTime(this));

    private void Tick(bool held, float deltaTime)
    {
        Vector2 position = transform.position;
        _moving = (_input != null && _input.MoveInput.sqrMagnitude > .001f) ||
            (_dash != null && _dash.IsDashing) ||
            (_body != null && _body.linearVelocity.sqrMagnitude > .001f) ||
            (position - _lastPosition).sqrMagnitude > .000001f;
        _lastPosition = position;

        if (!held) { _waitingForRelease = false; ConsumesRepairInput = false; }
        if (!CanInteract)
        {
            StopRepair(held);
            _target = default;
            return;
        }

        // 입력을 유지하는 동안 대상이 바뀌어 다른 터렛까지 연속 수리되지 않게 고정합니다.
        if (_heldTarget.Exists && (!IsReachable(_heldTarget) ||
            _heldTarget.IsDestroyed != _restoringHeldTarget ||
            (!_heldTarget.IsDestroyed && !_heldTarget.CanRepair))) StopRepair(held);
        _target = _heldTarget.Exists ? _heldTarget : FindNearest();
        if (held && IsEligible(_target) && (_target.IsDestroyed || _target.CanRepair))
            ConsumesRepairInput = true;

        if (!held)
        {
            StopRepair(false);
            return;
        }
        if (!CanRepair)
        {
            StopRepair(true);
            return;
        }
        if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
        {
            StopRepair(true);
            return;
        }

        if (!_heldTarget.Exists)
        {
            _heldTarget = _target;
            _restoringHeldTarget = _target.IsDestroyed;
            // 이미 예약된 근접 공격은 수리 진입 시 취소합니다.
            if (_spanner == null) _spanner = GetComponent<PlayerWeaponSelection>()?.Spanner;
            if (_spanner != null) _spanner.CancelPendingAttack();
        }
        bool progressed;
        if (_restoringHeldTarget)
            progressed = PlayerTurretRestoreProgress.TryAdvance(_heldTarget.Field, _maximumHealthFractionPerSecond, deltaTime);
        else
        {
            FieldTurretRepairResult result = _heldTarget.Repair(
                _heldTarget.MaxHealth * _maximumHealthFractionPerSecond, deltaTime);
            progressed = result == FieldTurretRepairResult.Repaired || result == FieldTurretRepairResult.Accumulating;
        }
        SetRepairing(progressed);
        if (!progressed || !_heldTarget.Exists || _heldTarget.IsDestroyed != _restoringHeldTarget ||
            _heldTarget.CurrentHealth >= _heldTarget.MaxHealth)
            StopRepair(true);
    }

    private PlayerTurretRepairTarget FindNearest()
    {
        PlayerTurretRepairTarget nearest = default, full = default;
        float distance = float.PositiveInfinity, fullDistance = float.PositiveInfinity;
        foreach (var field in FieldTurretInstanceRegistry.RegisteredInstances.Values)
            Consider(new PlayerTurretRepairTarget(field));
        foreach (var player in PlayerTurretHealth.ActiveInstances)
            Consider(new PlayerTurretRepairTarget(player));
        return nearest.Exists ? nearest : full;

        void Consider(PlayerTurretRepairTarget turret)
        {
            if (!IsEligible(turret)) return;
            float candidate = ((Vector2)turret.Transform.position - InteractionPosition).sqrMagnitude;
            bool needsRepair = turret.CurrentHealth < turret.MaxHealth;
            if (candidate >= (needsRepair ? distance : fullDistance) || !IsReachable(turret)) return;
            if (needsRepair) { nearest = turret; distance = candidate; }
            else { full = turret; fullDistance = candidate; }
        }
    }

    private bool IsEligible(PlayerTurretRepairTarget turret)
    {
        if (!turret.Exists || !turret.Source.isActiveAndEnabled || turret.MaxHealth <= 0 ||
            turret.Source.gameObject.scene != gameObject.scene) return false;
        if (turret.Field != null && (!turret.Field.IsInitialized || !turret.Field.HasStateAuthority ||
            turret.Field.IsLocked || turret.Field.IsManualOperationBlocked)) return false;
        if (turret.Player != null && (!turret.Player.IsAlive || !turret.Player.HasStateAuthority)) return false;
        var ownerTime = Presence?.TimeSource ?? GetComponent<FieldSimulationActor>()?.TimeSource;
        return ReferenceEquals(turret.Source.GetComponent<FieldSimulationActor>()?.TimeSource, ownerTime);
    }

    private bool IsReachable(PlayerTurretRepairTarget turret)
    {
        if (!IsEligible(turret)) return false;
        Vector2 point = turret.Player != null ? turret.Player.ApproachPoint(InteractionPosition) : (Vector2)turret.Transform.position;
        Vector2 direction = point - InteractionPosition;
        if (direction.sqrMagnitude < .0001f) direction = Vector2.down;
        // 마우스 조준과 무관하게 주변에서 고르되 기존 같은 씬·거리·장애물 도달 판정은 사용합니다.
        return PlayerSpannerTargeting.CanReach(transform, turret.Transform, point, direction, _repairRange, 180f, InteractionPosition);
    }

    private void SetRepairing(bool value)
    {
        if (IsRepairing == value) return;
        IsRepairing = value;
        // Rig.Update(-100)와 Motion.Update(100) 뒤에 시작/종료돼도 같은 프레임에 표시를 맞춥니다.
        if (_rig != null) _rig.RefreshPose(_input != null ? _input.AimDirection : Vector2.down);
        if (_motion != null) _motion.SetHeldRepair(value);
    }

    private void StopRepair(bool requireRelease)
    {
        _heldTarget = default;
        _waitingForRelease |= requireRelease;
        SetRepairing(false);
    }

    private void OnDisable()
    {
        StopRepair(true);
        _target = default;
        ConsumesRepairInput = false;
    }
}

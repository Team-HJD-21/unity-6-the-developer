using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Selects directional Animator states without changing combat or physics.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(PlayerInputHandler), typeof(Animator))]
[DefaultExecutionOrder(100)]
public sealed partial class PlayerSpriteMotion : MonoBehaviour
{
    [SerializeField] private PlayerMotionSet _motions;
    [SerializeField] private SpriteRenderer _renderer;
    [FormerlySerializedAs("_legacyAnimator")]
    [SerializeField] private Animator _animator;
    private PlayerInputHandler _input;
    private PlayerWeaponRig _rig;
    private PlayerControlState _controls;
    private PlayerWeaponSelection _selection;
    private PlayerSkillController _skills;
    private PlayerTurretRepair _repair;
    private PlayerMotion _motion;
    private PlayerAimFacing _facing;
    private float _elapsed;
    private bool _ownsDisplay;
    private bool _restart;
    private bool _hoveringAction;
    private PlayerLateralLean _lateralLean;
    private bool _animatorWasEnabled;
    private Sprite _previousSprite;
    private bool _previousFlip;
    private float _impactPauseRemaining;
    private float _speedBeforePause;
    private bool _usesToolPoses;
    private bool _heldRepair;
    public bool IsHoldingRepair => _heldRepair;
    public uint HeldRepairImpactSequence { get; private set; }

    public bool IsPlayingToolAction => _ownsDisplay &&
        (_motion == PlayerMotion.Attack || _motion == PlayerMotion.Repair);
    public bool OwnsDisplay => _ownsDisplay;
    public bool SupportsEightWayRanged => _motions != null && _motions.HasEightWayRanged;
    public bool SupportsEightWayTools => _motions != null && _motions.HasEightWayTools;
    public bool SupportsDiagonalBackpedal => SupportsEightWayRanged && _motions.HasDiagonalBackpedal;
    public bool IsFiring => _ownsDisplay && _motion == PlayerMotion.Fire;
    public bool IsPlayingDashAnimation => _ownsDisplay &&
        (_motion == PlayerMotion.Dash || _motion == PlayerMotion.ToolDash);
    private bool IsAction => IsPlayingToolAction || _motion == PlayerMotion.Fire;
    private bool HasSpanner => _heldRepair || (_selection != null && _selection.CurrentWeapon == WeaponType.Spanner);
    // 우주선은 전투(CanAct)가 막혀도 이동(CanMove)은 허용합니다. 이동 자세도 같은 기준을 따릅니다.
    private bool HasMovementInput => _input.MoveInput.sqrMagnitude > .001f &&
        (_controls == null || _controls.CanMove);
    private AnimationClip CurrentClip => _motions.GetClip(_motion, _hoveringAction, _lateralLean).Animation(_facing);
    private string LeanSuffix => _lateralLean == PlayerLateralLean.Neutral ? "" : $"Lean{_lateralLean}";
    private int CurrentStateHash => Animator.StringToHash(
        $"Base Layer.{(_hoveringAction ? "Hover" : "")}{_motion}{LeanSuffix}_{_facing}");

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        if (_animator == null) _animator = GetComponent<Animator>();
        _input = GetComponent<PlayerInputHandler>();
        _controls = GetComponent<PlayerControlState>();
        _selection = GetComponent<PlayerWeaponSelection>();
        _skills = GetComponent<PlayerSkillController>();
        _repair = GetComponent<PlayerTurretRepair>();
        _spaceManColliders = GetComponent<PlayerDirectionalColliders>();
        if (_spaceManShadow != null)
            _spaceManShadowRenderer = _spaceManShadow.GetComponentInChildren<SpriteRenderer>(true);
        _rig = GetComponentInChildren<PlayerWeaponRig>(true);
    }

    private void OnEnable()
    {
        if (_motions == null || !_motions.HasAnimatorConfiguration ||
            _animator.runtimeAnimatorController != _motions.Controller)
        {
            Debug.LogWarning("[PlayerSpriteMotion] Connect every weapon pose clip and its matching Animator Controller.", this);
            return;
        }
        _previousSprite = _renderer.sprite;
        _previousFlip = _renderer.flipX;
        _animatorWasEnabled = _animator.enabled;
        _animator.enabled = true;
        _ownsDisplay = true;
        _usesToolPoses = HasSpanner;
        if (_selection != null) _selection.WeaponChanged += OnWeaponChanged;
        StartMotion(HasSpanner ? PlayerMotion.ToolIdle : PlayerMotion.Idle);
        UpdateAnimatorState();
        if (UsesSpaceManBody) RefreshSpaceManBody();
    }

    private void OnWeaponChanged(WeaponType weapon)
    {
        bool tool = _heldRepair || weapon == WeaponType.Spanner;
        if (!_ownsDisplay || !isActiveAndEnabled || tool == _usesToolPoses) return;
        _usesToolPoses = tool;
        EndImpactPause();
        StartMotion(tool
            ? (HasMovementInput ? PlayerMotion.ToolMove : PlayerMotion.ToolIdle)
            : (HasMovementInput ? PlayerMotion.Move : PlayerMotion.Idle));
        // 무기 선택은 이 컴포넌트의 Update 뒤에도 발생합니다. 같은 프레임에 새 무기의 몸체를 평가합니다.
        UpdateAnimatorState();
        _animator.Update(0f);
    }

    public bool PlayToolAction(bool repairing)
    {
        if (!_ownsDisplay || !isActiveAndEnabled) return false;
        StartMotion(repairing ? PlayerMotion.Repair : PlayerMotion.Attack);
        return true;
    }

    /// <summary>무기 선택·실제 공격 판정 없이 일반 스패너 공격 자세를 반복합니다.</summary>
    public void SetHeldRepair(bool repairing)
    {
        if (_heldRepair == repairing) return;
        _heldRepair = repairing;
        if (!_ownsDisplay || !isActiveAndEnabled) return;
        _usesToolPoses = HasSpanner;
        EndImpactPause();
        StartMotion(repairing ? PlayerMotion.Attack : HasSpanner
            ? (HasMovementInput ? PlayerMotion.ToolMove : PlayerMotion.ToolIdle)
            : (HasMovementInput ? PlayerMotion.Move : PlayerMotion.Idle));
        UpdateAnimatorState();
        _animator.Update(0f);
    }

    public void PlayRangedShot()
    {
        if (_ownsDisplay && !HasSpanner) StartMotion(PlayerMotion.Fire);
    }

    /// <summary>짧은 부스트 모션을 재생합니다. 이동 속도와 무적 상태는 변경하지 않습니다.</summary>
    public bool TryPlayDashAnimation()
    {
        if (!_ownsDisplay || !isActiveAndEnabled || !_motions.HasDashAnimations ||
            Time.timeScale <= 0f || (_controls != null && !_controls.CanAct) ||
            (_skills != null && !_skills.CanUse) || IsAction || IsPlayingDashAnimation)
            return false;

        StartMotion(HasSpanner ? PlayerMotion.ToolDash : PlayerMotion.Dash);
        UpdateAnimatorState();
        return true;
    }

    public void CancelToolAction()
    {
        EndImpactPause();
        if (IsPlayingToolAction) StartMotion(PlayerMotion.Idle);
    }

    public float ToolImpactDelay(bool repairing)
    {
        if (!_ownsDisplay) return 0f;
        PlayerMotion motion = repairing ? PlayerMotion.Repair : PlayerMotion.Attack;
        return _motions.GetClip(motion, HasMovementInput).Animation(_facing).length * .5f;
    }

    public void PauseAtImpact(float seconds)
    {
        if (!IsPlayingToolAction || seconds <= 0f) return;
        // Hold the actual strike pose, even if this Update runs before Animator evaluation.
        UpdateAnimatorState();
        _elapsed = CurrentClip.length * .5f;
        _animator.Play(CurrentStateHash, 0, .5f);
        _animator.Update(0f);
        if (_impactPauseRemaining <= 0f) _speedBeforePause = _animator.speed;
        _impactPauseRemaining = Mathf.Clamp(seconds, 0f, .1f);
        _animator.speed = 0f;
    }

    private void EndImpactPause()
    {
        if (_impactPauseRemaining <= 0f) return;
        _impactPauseRemaining = 0f;
        if (_animator != null) _animator.speed = _speedBeforePause;
    }

    private void StartMotion(PlayerMotion motion)
    {
        _motion = motion;
        _elapsed = 0f;
        _restart = true;
    }

    private void Update()
    {
        if (!_ownsDisplay) return;
        bool canAct = _controls == null || _controls.CanAct;
        if (!canAct) CancelToolAction();
        if (!canAct && IsPlayingDashAnimation) StartMotion(PlayerMotion.Idle);
        if (_impactPauseRemaining > 0f)
        {
            float remaining = _impactPauseRemaining - Time.deltaTime;
            if (remaining > 0f) { _impactPauseRemaining = remaining; return; }
            EndImpactPause();
        }
        AnimationClip clip = CurrentClip;
        if (_heldRepair && canAct)
        {
            if (_motion != PlayerMotion.Attack || _elapsed >= clip.length) StartMotion(PlayerMotion.Attack);
            UpdateAnimatorState();
            AdvanceHeldRepairTime(Time.deltaTime);
            return;
        }
        bool weaponChangedDuringDash = IsPlayingDashAnimation &&
            ((_motion == PlayerMotion.ToolDash) != HasSpanner);
        if ((!IsAction && !IsPlayingDashAnimation) || _elapsed >= clip.length ||
            (_motion == PlayerMotion.Fire && HasSpanner) || weaponChangedDuringDash)
        {
            bool moving = HasMovementInput;
            PlayerMotion next = HasSpanner
                ? (moving ? PlayerMotion.ToolMove : PlayerMotion.ToolIdle)
                : (moving ? PlayerMotion.Move : PlayerMotion.Idle);
            if (next != _motion) StartMotion(next);
        }
        UpdateAnimatorState();
        _elapsed += Time.deltaTime;
    }

    private void AdvanceHeldRepairTime(float seconds)
    {
        float impact = CurrentClip.length * .5f;
        float previous = _elapsed;
        _elapsed += seconds;
        if (previous < impact && _elapsed >= impact)
            HeldRepairImpactSequence = unchecked(HeldRepairImpactSequence + 1);
    }

    private void UpdateAnimatorState()
    {
        bool supportsEightDirections = HasSpanner ? SupportsEightWayTools : SupportsEightWayRanged;
        Vector2 lookDirection = _input.AimDirection;
        bool repairFacing = _heldRepair && _repair != null && _repair.TryGetFacingDirection(out lookDirection);
        PlayerAimFacing facing = repairFacing
            ? (supportsEightDirections ? PlayerAimUtility.GetEightWayFacing(lookDirection) : PlayerAimUtility.GetFacing(lookDirection))
            : _rig != null ? _rig.Facing : PlayerAimUtility.GetFacing(_input.AimDirection);
        if (PlayerAimUtility.IsDiagonal(facing) && !supportsEightDirections)
            facing = PlayerAimUtility.GetFacing(repairFacing ? lookDirection : _input.AimDirection);
        bool hoveringAction = IsAction && HasMovementInput;
        PlayerLateralLean lateralLean = GetMovementLean(facing);
        if (!_restart && facing == _facing && hoveringAction == _hoveringAction && lateralLean == _lateralLean) return;
        // Turning or starting/stopping movement preserves the action phase and impact timing.
        float previousLength = CurrentClip.length;
        float phase = _restart ? 0f : _elapsed / Mathf.Max(.01f, previousLength);
        _facing = facing;
        _hoveringAction = hoveringAction;
        _lateralLean = lateralLean;
        _elapsed = phase * CurrentClip.length;
        float normalizedTime = IsAction || IsPlayingDashAnimation ? Mathf.Clamp01(phase) : Mathf.Repeat(phase, 1f);
        _animator.Play(CurrentStateHash, 0, normalizedTime);
        _restart = false;
    }

    private PlayerLateralLean GetMovementLean(PlayerAimFacing facing)
    {
        if (!HasMovementInput || HasSpanner ||
            (_motion != PlayerMotion.Move && _motion != PlayerMotion.Fire))
            return PlayerLateralLean.Neutral;
        if (PlayerAimUtility.IsDiagonal(facing))
            return SupportsDiagonalBackpedal && PlayerAimUtility.IsMovingBackward(facing, _input.MoveInput)
                ? PlayerLateralLean.Back : PlayerLateralLean.Neutral;
        if (_input.MoveInput.x < -.01f) return PlayerLateralLean.Left;
        if (_input.MoveInput.x > .01f) return PlayerLateralLean.Right;
        return PlayerLateralLean.Neutral;
    }

    private void OnDisable()
    {
        if (_selection != null) _selection.WeaponChanged -= OnWeaponChanged;
        if (!_ownsDisplay) return;
        EndImpactPause();
        _ownsDisplay = false;
        if (_renderer != null)
        {
            _renderer.sprite = _previousSprite;
            _renderer.flipX = _previousFlip;
        }
        if (_animator != null) _animator.enabled = _animatorWasEnabled;
    }
}

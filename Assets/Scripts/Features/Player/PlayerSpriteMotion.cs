using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Selects directional Animator states without changing combat or physics.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(PlayerInputHandler), typeof(Animator))]
[DefaultExecutionOrder(100)]
public sealed class PlayerSpriteMotion : MonoBehaviour
{
    [SerializeField] private PlayerMotionSet _motions;
    [SerializeField] private SpriteRenderer _renderer;
    [FormerlySerializedAs("_legacyAnimator")]
    [SerializeField] private Animator _animator;
    private PlayerInputHandler _input;
    private PlayerWeaponRig _rig;
    private PlayerControlState _controls;
    private PlayerAttack _attack;
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

    public bool IsPlayingToolAction => _ownsDisplay &&
        (_motion == PlayerMotion.Attack || _motion == PlayerMotion.Repair);
    public bool OwnsDisplay => _ownsDisplay;
    private bool IsAction => IsPlayingToolAction || _motion == PlayerMotion.Fire;
    private bool HasSpanner => _attack != null && _attack.CurrentWeapon == WeaponType.Spanner;
    private bool HasMovementInput => _input.MoveInput.sqrMagnitude > .001f &&
        (_controls == null || _controls.CanAct);
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
        _attack = GetComponent<PlayerAttack>();
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
        StartMotion(PlayerMotion.Idle);
        UpdateAnimatorState();
    }

    public bool PlayToolAction(bool repairing)
    {
        if (!_ownsDisplay || !isActiveAndEnabled) return false;
        StartMotion(repairing ? PlayerMotion.Repair : PlayerMotion.Attack);
        return true;
    }

    public void PlayRangedShot()
    {
        if (_ownsDisplay && !HasSpanner) StartMotion(PlayerMotion.Fire);
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
        if (_impactPauseRemaining > 0f)
        {
            float remaining = _impactPauseRemaining - Time.deltaTime;
            if (remaining > 0f) { _impactPauseRemaining = remaining; return; }
            EndImpactPause();
        }
        AnimationClip clip = CurrentClip;
        if (!IsAction || _elapsed >= clip.length || (_motion == PlayerMotion.Fire && HasSpanner))
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

    private void UpdateAnimatorState()
    {
        PlayerAimFacing facing = _rig != null ? _rig.Facing : PlayerAimUtility.GetFacing(_input.AimDirection);
        bool hoveringAction = IsAction && HasMovementInput;
        PlayerLateralLean lateralLean = GetMovementLean();
        if (!_restart && facing == _facing && hoveringAction == _hoveringAction && lateralLean == _lateralLean) return;
        // Turning or starting/stopping movement preserves the action phase and impact timing.
        float previousLength = CurrentClip.length;
        float phase = _restart ? 0f : _elapsed / Mathf.Max(.01f, previousLength);
        _facing = facing;
        _hoveringAction = hoveringAction;
        _lateralLean = lateralLean;
        _elapsed = phase * CurrentClip.length;
        float normalizedTime = IsAction ? Mathf.Clamp01(phase) : Mathf.Repeat(phase, 1f);
        _animator.Play(CurrentStateHash, 0, normalizedTime);
        _restart = false;
    }

    private PlayerLateralLean GetMovementLean()
    {
        if (!HasMovementInput || HasSpanner || (_motion != PlayerMotion.Move && _motion != PlayerMotion.Fire))
            return PlayerLateralLean.Neutral;
        if (_input.MoveInput.x < -.01f) return PlayerLateralLean.Left;
        if (_input.MoveInput.x > .01f) return PlayerLateralLean.Right;
        return PlayerLateralLean.Neutral;
    }

    private void OnDisable()
    {
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

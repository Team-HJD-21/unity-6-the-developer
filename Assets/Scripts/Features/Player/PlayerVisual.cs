using UnityEngine;

/// <summary>총기와 같은 방향 기준으로 몸체 애니메이션을 표시합니다.</summary>
[DisallowMultipleComponent]
public class PlayerVisual : MonoBehaviour
{
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerWeaponRig _weaponRig;
    [SerializeField] private PlayerDirectionalColliders _directionalColliders;
    private PlayerSkillController _skills;
    private PlayerControlState _controls;
    private PlayerSpriteMotion _spriteMotion;
    private bool _hasSide, _hasIdle, _hasUp;
    private static readonly int IsSideHash = Animator.StringToHash("isSide");
    private static readonly int IsIdleHash = Animator.StringToHash("isIdle");
    private static readonly int IsUpHash = Animator.StringToHash("isUp");

    private void Awake()
    {
        if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (_weaponRig == null) _weaponRig = GetComponentInChildren<PlayerWeaponRig>(true);
        if (_directionalColliders == null) _directionalColliders = GetComponent<PlayerDirectionalColliders>();
        _skills = GetComponent<PlayerSkillController>();
        _controls = GetComponent<PlayerControlState>();
        _spriteMotion = GetComponent<PlayerSpriteMotion>();
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Bool) continue;
                if (parameter.nameHash == IsSideHash) _hasSide = true;
                if (parameter.nameHash == IsIdleHash) _hasIdle = true;
                if (parameter.nameHash == IsUpHash) _hasUp = true;
            }
        ApplyFacing(PlayerAimFacing.Down);
    }

    private void Update()
    {
        // Rig 없는 기존 구성도 우주선의 이동 허용과 전투 금지를 구분합니다.
        bool canFace = _controls != null ? _controls.CanMove : _skills == null || _skills.CanUse;
        var facing = _weaponRig != null ? _weaponRig.Facing :
            (!canFace || (_skills != null && _skills.IsSelectionOpen) ? PlayerAimFacing.Down :
            PlayerAimUtility.GetFacing(inputHandler != null ? inputHandler.AimDirection : Vector2.down));
        ApplyFacing(facing);
    }

    private void ApplyFacing(PlayerAimFacing facing)
    {
        if (_directionalColliders != null)
            _directionalColliders.SetFacing(_spriteMotion != null && _spriteMotion.UsesSpaceManBody
                ? _spriteMotion.SpaceManFacing : facing);
        // 구형 Animator의 bool 파라미터만 4방향으로 변환합니다.
        if (PlayerAimUtility.IsDiagonal(facing))
            facing = PlayerAimUtility.GetFacing(inputHandler != null ? inputHandler.AimDirection : Vector2.down);
        if (spriteRenderer != null && (_spriteMotion == null || !_spriteMotion.OwnsDisplay))
            spriteRenderer.flipX = facing == PlayerAimFacing.Right;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        if (_hasSide) animator.SetBool(IsSideHash, facing == PlayerAimFacing.Left || facing == PlayerAimFacing.Right);
        if (_hasIdle) animator.SetBool(IsIdleHash, facing == PlayerAimFacing.Down);
        if (_hasUp) animator.SetBool(IsUpHash, facing == PlayerAimFacing.Up);
    }
}

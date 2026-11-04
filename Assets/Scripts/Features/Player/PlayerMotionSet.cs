using System;
using UnityEngine;

public enum PlayerMotion { Idle, Move, Attack, Repair, ToolIdle, ToolMove, Fire, Dash, ToolDash }
public enum PlayerLateralLean { Neutral = 0, Left = 1, Right = 2, Back = 3 }

/// <summary>행동·방향에 맞는 AnimationClip을 연결합니다. 프레임과 재생 시간은 클립이 소유합니다.</summary>
[CreateAssetMenu(menuName = "Player/Motion Set")]
public sealed class PlayerMotionSet : ScriptableObject
{
    [Serializable]
    public sealed class Clip
    {
        public AnimationClip downAnimation;
        public AnimationClip upAnimation;
        public AnimationClip leftAnimation;
        public AnimationClip rightAnimation;
        public AnimationClip downLeftAnimation;
        public AnimationClip downRightAnimation;
        public AnimationClip upLeftAnimation;
        public AnimationClip upRightAnimation;

        public AnimationClip Animation(PlayerAimFacing facing)
        {
            switch (facing)
            {
                case PlayerAimFacing.DownLeft: return downLeftAnimation != null ? downLeftAnimation : leftAnimation;
                case PlayerAimFacing.DownRight: return downRightAnimation != null ? downRightAnimation : rightAnimation;
                case PlayerAimFacing.UpLeft: return upLeftAnimation != null ? upLeftAnimation : upAnimation;
                case PlayerAimFacing.UpRight: return upRightAnimation != null ? upRightAnimation : upAnimation;
                case PlayerAimFacing.Up: return upAnimation;
                case PlayerAimFacing.Left: return leftAnimation;
                case PlayerAimFacing.Right: return rightAnimation;
                default: return downAnimation;
            }
        }

        public bool IsConfigured => IsPlayable(downAnimation) && IsPlayable(upAnimation)
            && IsPlayable(leftAnimation) && IsPlayable(rightAnimation);
        public bool HasDiagonalDirections => IsPlayable(downLeftAnimation) &&
            IsPlayable(downRightAnimation) && IsPlayable(upLeftAnimation) && IsPlayable(upRightAnimation);
        public bool HasEightDirections => IsConfigured && HasDiagonalDirections;

        private static bool IsPlayable(AnimationClip animation) => animation != null
            && !float.IsNaN(animation.length) && !float.IsInfinity(animation.length) && animation.length > 0f;
    }

    [SerializeField] private Clip _idle = new Clip();
    [SerializeField] private Clip _move = new Clip();
    [SerializeField] private Clip _attack = new Clip();
    [SerializeField] private Clip _repair = new Clip();
    [SerializeField] private Clip _toolIdle = new Clip();
    [SerializeField] private Clip _toolMove = new Clip();
    [SerializeField] private Clip _fire = new Clip();
    [SerializeField] private Clip _hoverFire = new Clip();
    [SerializeField] private Clip _hoverAttack = new Clip();
    [SerializeField] private Clip _hoverRepair = new Clip();
    [SerializeField] private Clip _moveLeanLeft = new Clip();
    [SerializeField] private Clip _moveLeanRight = new Clip();
    [SerializeField] private Clip _hoverFireLeanLeft = new Clip();
    [SerializeField] private Clip _hoverFireLeanRight = new Clip();
    [SerializeField] private Clip _moveBackpedal = new Clip();
    [SerializeField] private Clip _hoverFireBackpedal = new Clip();
    [SerializeField] private Clip _dash = new Clip();
    [SerializeField] private Clip _toolDash = new Clip();
    [SerializeField] private RuntimeAnimatorController _controller;

    public RuntimeAnimatorController Controller => _controller;
    public bool HasAnimatorConfiguration => _controller != null && IsConfigured;
    public bool HasEightWayRanged => _idle != null && _idle.HasEightDirections &&
        _move != null && _move.HasEightDirections && _fire != null && _fire.HasEightDirections &&
        _hoverFire != null && _hoverFire.HasEightDirections;
    public bool HasEightWayTools => _toolIdle != null && _toolIdle.HasEightDirections &&
        _toolMove != null && _toolMove.HasEightDirections &&
        _attack != null && _attack.HasEightDirections && _repair != null && _repair.HasEightDirections &&
        _hoverAttack != null && _hoverAttack.HasEightDirections &&
        _hoverRepair != null && _hoverRepair.HasEightDirections;
    public bool HasDiagonalBackpedal => _moveBackpedal != null && _moveBackpedal.HasDiagonalDirections &&
        _hoverFireBackpedal != null && _hoverFireBackpedal.HasDiagonalDirections;
    public bool HasDashAnimations => _dash != null && _dash.HasEightDirections &&
        _toolDash != null && _toolDash.HasEightDirections;

    public bool IsConfigured => _idle != null && _move != null && _attack != null && _repair != null &&
        _idle.IsConfigured && _move.IsConfigured && _attack.IsConfigured && _repair.IsConfigured &&
        _toolIdle != null && _toolIdle.IsConfigured && _toolMove != null && _toolMove.IsConfigured &&
        _fire != null && _fire.IsConfigured &&
        _hoverFire != null && _hoverFire.IsConfigured &&
        _hoverAttack != null && _hoverAttack.IsConfigured &&
        _hoverRepair != null && _hoverRepair.IsConfigured &&
        _moveLeanLeft != null && _moveLeanLeft.IsConfigured &&
        _moveLeanRight != null && _moveLeanRight.IsConfigured &&
        _hoverFireLeanLeft != null && _hoverFireLeanLeft.IsConfigured &&
        _hoverFireLeanRight != null && _hoverFireLeanRight.IsConfigured;

    public Clip GetClip(PlayerMotion motion) => GetClip(motion, false);

    /// <summary>같은 행동을 유지하면서 이동 중에 사용할 부유 자세를 선택합니다.</summary>
    public Clip GetClip(PlayerMotion motion, bool hovering)
        => GetClip(motion, hovering, PlayerLateralLean.Neutral);

    /// <summary>좌우 이동 기울임 또는 대각선 후진에 맞는 총기 자세를 선택합니다.</summary>
    public Clip GetClip(PlayerMotion motion, bool hovering, PlayerLateralLean lean)
    {
        if (lean == PlayerLateralLean.Back && HasDiagonalBackpedal)
        {
            if (motion == PlayerMotion.Move) return _moveBackpedal;
            if (motion == PlayerMotion.Fire && hovering) return _hoverFireBackpedal;
        }
        if (lean == PlayerLateralLean.Left || lean == PlayerLateralLean.Right)
        {
            if (motion == PlayerMotion.Move)
                return lean == PlayerLateralLean.Left ? _moveLeanLeft : _moveLeanRight;
            if (motion == PlayerMotion.Fire && hovering)
                return lean == PlayerLateralLean.Left ? _hoverFireLeanLeft : _hoverFireLeanRight;
        }
        if (hovering)
        {
            switch (motion)
            {
                case PlayerMotion.Fire: return _hoverFire;
                case PlayerMotion.Attack: return _hoverAttack;
                case PlayerMotion.Repair: return _hoverRepair;
            }
        }
        switch (motion)
        {
            case PlayerMotion.Move: return _move;
            case PlayerMotion.Attack: return _attack;
            case PlayerMotion.Repair: return _repair;
            case PlayerMotion.ToolIdle: return _toolIdle;
            case PlayerMotion.ToolMove: return _toolMove;
            case PlayerMotion.Fire: return _fire;
            case PlayerMotion.Dash: return HasDashAnimations ? _dash : _move;
            case PlayerMotion.ToolDash: return HasDashAnimations ? _toolDash : _toolMove;
            default: return _idle;
        }
    }
}

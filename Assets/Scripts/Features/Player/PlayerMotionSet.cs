using System;
using UnityEngine;

public enum PlayerMotion { Idle, Move, Attack, Repair, ToolIdle, ToolMove, Fire }
public enum PlayerLateralLean { Neutral, Left, Right }

/// <summary>플레이어의 네 방향 프레임과 재생 시간만 보관합니다. 전투 판정을 수행하지 않습니다.</summary>
[CreateAssetMenu(menuName = "Player/Motion Set")]
public sealed class PlayerMotionSet : ScriptableObject
{
    [Serializable]
    public sealed class Clip
    {
        [Min(.01f)] public float duration = .4f;
        public Sprite[] down = Array.Empty<Sprite>();
        public Sprite[] up = Array.Empty<Sprite>();
        public Sprite[] left = Array.Empty<Sprite>();
        public Sprite[] right = Array.Empty<Sprite>();
        public AnimationClip downAnimation;
        public AnimationClip upAnimation;
        public AnimationClip leftAnimation;
        public AnimationClip rightAnimation;

        public AnimationClip Animation(PlayerAimFacing facing)
        {
            switch (facing)
            {
                case PlayerAimFacing.Up: return upAnimation;
                case PlayerAimFacing.Left: return leftAnimation;
                case PlayerAimFacing.Right: return rightAnimation;
                default: return downAnimation;
            }
        }

        public bool HasAnimations => downAnimation != null && upAnimation != null &&
            leftAnimation != null && rightAnimation != null;

        public Sprite[] Frames(PlayerAimFacing facing)
        {
            switch (facing)
            {
                case PlayerAimFacing.Up: return up;
                case PlayerAimFacing.Left: return left;
                case PlayerAimFacing.Right: return right;
                default: return down;
            }
        }

        public bool IsConfigured
        {
            get
            {
                foreach (var frames in new[] { down, up, left, right })
                {
                    if (frames == null || frames.Length == 0) return false;
                    foreach (Sprite sprite in frames) if (sprite == null) return false;
                }
                return !float.IsNaN(duration) && !float.IsInfinity(duration) && duration > 0f;
            }
        }

        public Sprite Sample(PlayerAimFacing facing, float elapsed, bool loop)
        {
            Sprite[] frames = Frames(facing);
            if (frames == null || frames.Length == 0) return null;
            float progress = elapsed / Mathf.Max(.01f, duration);
            progress = loop ? Mathf.Repeat(progress, 1f) : Mathf.Clamp01(progress);
            return frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(progress * frames.Length))];
        }
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
    [SerializeField] private RuntimeAnimatorController _controller;

    public RuntimeAnimatorController Controller => _controller;
    public bool HasAnimatorConfiguration => IsConfigured && _controller != null &&
        _idle.HasAnimations && _move.HasAnimations && _attack.HasAnimations && _repair.HasAnimations &&
        _toolIdle.HasAnimations && _toolMove.HasAnimations && _fire.HasAnimations &&
        _hoverFire.HasAnimations && _hoverAttack.HasAnimations && _hoverRepair.HasAnimations &&
        _moveLeanLeft.HasAnimations && _moveLeanRight.HasAnimations &&
        _hoverFireLeanLeft.HasAnimations && _hoverFireLeanRight.HasAnimations;

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

    /// <summary>조준 방향과 별개로 좌우 이동에 따른 총기 자세를 선택합니다.</summary>
    public Clip GetClip(PlayerMotion motion, bool hovering, PlayerLateralLean lean)
    {
        if (lean != PlayerLateralLean.Neutral)
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
            default: return _idle;
        }
    }
}

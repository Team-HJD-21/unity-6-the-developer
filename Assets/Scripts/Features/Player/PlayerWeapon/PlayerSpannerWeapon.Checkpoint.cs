// 무기 writer가 진행 중인 렌치 타격 의도와 남은 impact/cooldown을 보존하도록 연결합니다.
// 이미 발생한 피해는 다시 적용하지 않으며 기한이 남은 타격만 새 시간 기준에 이어 붙입니다.
using System;
using System.IO;
using UnityEngine;

public sealed partial class PlayerSpannerWeapon
{
    internal CheckpointState CaptureCheckpoint() => new(_pendingStrike, _repairIntent, _swingDirection, _swingRemaining,
        Math.Max(0, _nextAttackTime - GameSeconds), _pendingStrike ? Math.Max(0, _impactAt - GameSeconds) : 0);
    internal void RestoreCheckpoint(CheckpointState state)
    {
        _pendingStrike = state.Pending; _repairIntent = state.Repair;
        _swingDirection = state.Direction; _swingRemaining = state.SwingRemaining;
        _nextAttackTime = GameSeconds + state.CooldownRemaining; _impactAt = GameSeconds + state.ImpactRemaining;
    }
    internal sealed class CheckpointState
    {
        public readonly bool Pending, Repair;
        public readonly Vector2 Direction;
        public readonly float SwingRemaining;
        public readonly double CooldownRemaining, ImpactRemaining;
        public CheckpointState(bool pending, bool repair, Vector2 direction, float swing, double cooldown, double impact)
        {
            if (float.IsNaN(direction.x) || float.IsInfinity(direction.x) || float.IsNaN(direction.y) || float.IsInfinity(direction.y) ||
                float.IsNaN(swing) || float.IsInfinity(swing) || swing < 0 ||
                double.IsNaN(cooldown) || double.IsInfinity(cooldown) || cooldown < 0 ||
                double.IsNaN(impact) || double.IsInfinity(impact) || impact < 0 || (pending && !PlayerAimUtility.IsValidDirection(direction)))
                throw new InvalidDataException("Invalid saved wrench attack.");
            Pending = pending; Repair = repair; Direction = direction; SwingRemaining = swing; CooldownRemaining = cooldown; ImpactRemaining = impact;
        }
        public void Write(BinaryWriter writer)
        { writer.Write(Pending); writer.Write(Repair); writer.Write(Direction.x); writer.Write(Direction.y); writer.Write(SwingRemaining); writer.Write(CooldownRemaining); writer.Write(ImpactRemaining); }
        public static CheckpointState Read(BinaryReader reader) => new(reader.ReadBoolean(), reader.ReadBoolean(),
            new Vector2(reader.ReadSingle(), reader.ReadSingle()), reader.ReadSingle(), reader.ReadDouble(), reader.ReadDouble());
    }
}

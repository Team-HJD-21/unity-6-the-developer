// Player actor writer가 대시의 진행·방향·속도와 재사용 대기를 함께 보존하도록 연결합니다.
// 이동/대시를 다시 발동하지 않으며 Rigidbody 복원은 PlayerInfo가 담당합니다.
using System;
using System.IO;
using UnityEngine;

public sealed partial class PlayerDash
{
    internal CheckpointState CaptureCheckpoint() => new(_state.Remaining, _state.CooldownRemaining,
        _state.CooldownDuration, _lastDirection, _direction, _speed);

    internal void RestoreCheckpoint(CheckpointState state)
    {
        _state.Restore(state.Remaining, state.CooldownRemaining, state.CooldownDuration);
        _lastDirection = state.LastDirection;
        _direction = state.Direction;
        _speed = state.Speed;
    }

    internal sealed class CheckpointState
    {
        public readonly float Remaining, CooldownRemaining, CooldownDuration, Speed;
        public readonly Vector2 LastDirection, Direction;
        public CheckpointState(float remaining, float cooldownRemaining, float cooldownDuration,
            Vector2 lastDirection, Vector2 direction, float speed)
        {
            if (!float.IsFinite(remaining) || remaining < 0 || !float.IsFinite(cooldownRemaining) || cooldownRemaining < 0 ||
                !float.IsFinite(cooldownDuration) || cooldownDuration < cooldownRemaining || !float.IsFinite(speed) || speed < 0 ||
                !float.IsFinite(lastDirection.x) || !float.IsFinite(lastDirection.y) || !float.IsFinite(direction.x) || !float.IsFinite(direction.y) ||
                (remaining > 0 && (speed <= 0 || !PlayerAimUtility.IsValidDirection(direction))))
                throw new InvalidDataException("Invalid saved Player dash state.");
            Remaining = remaining;
            CooldownRemaining = cooldownRemaining;
            CooldownDuration = cooldownDuration;
            LastDirection = lastDirection;
            Direction = direction;
            Speed = speed;
        }
        public void Write(BinaryWriter writer)
        {
            writer.Write(Remaining); writer.Write(CooldownRemaining); writer.Write(CooldownDuration);
            writer.Write(LastDirection.x); writer.Write(LastDirection.y);
            writer.Write(Direction.x); writer.Write(Direction.y); writer.Write(Speed);
        }
        public static CheckpointState Read(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
            new Vector2(reader.ReadSingle(), reader.ReadSingle()), new Vector2(reader.ReadSingle(), reader.ReadSingle()), reader.ReadSingle());
    }
}

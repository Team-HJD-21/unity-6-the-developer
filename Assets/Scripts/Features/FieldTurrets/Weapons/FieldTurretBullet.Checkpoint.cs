// 캐논이 실제로 소유한 탄환의 비행 진행을 저장합니다.
// 복원은 발사를 다시 하지 않으며 원래 발사자의 수명과 남은 시간을 이어갑니다.
using System;
using System.IO;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Contracts;
using UnityEngine;

public partial class FieldTurretBullet
{
    internal string CheckpointOwnerId;
    internal string CheckpointSourceDefinition;

    internal State CaptureCheckpoint()
    {
        if (!_isInitialized || _hasHit || !isActiveAndEnabled) throw new InvalidOperationException("Capture only live turret bullets.");
        return new State(CheckpointSourceDefinition, CheckpointTransformState.Capture(transform), _direction, _bulletDamage,
            Math.Max(0, _expiresAt - FieldSimulationTime.Seconds(this)));
    }

    internal void RestoreCheckpoint(State state, Transform owner)
    {
        CheckpointSourceDefinition = state.SourceDefinition;
        state.Pose.Apply(transform); _target = owner; _direction = state.Direction;
        _bulletDamage = state.Damage; _expiresAt = FieldSimulationTime.Seconds(this) + state.Remaining;
        _hasHit = false; _isInitialized = true;
    }

    internal sealed class State
    {
        public readonly string SourceDefinition;
        public readonly CheckpointTransformState Pose;
        public readonly Vector3 Direction;
        public readonly float Damage;
        public readonly double Remaining;
        public State(string sourceDefinition, CheckpointTransformState pose, Vector3 direction, float damage, double remaining)
        {
            if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z) ||
                !float.IsFinite(damage) || damage < 0 || !double.IsFinite(remaining) || remaining < 0 || remaining > 3)
                throw new InvalidDataException("Invalid turret bullet state.");
            SourceDefinition = sourceDefinition;
            if (string.IsNullOrWhiteSpace(sourceDefinition)) throw new InvalidDataException("Missing projectile source definition.");
            Pose = pose; Direction = direction; Damage = damage; Remaining = remaining;
        }
        public void Write(BinaryWriter w, ICheckpointPayloadCodec codec)
        { codec.WriteText(w, SourceDefinition); Pose.Write(w); w.Write(Direction.x); w.Write(Direction.y); w.Write(Direction.z); w.Write(Damage); w.Write(Remaining); }
        public static State Read(BinaryReader r, ICheckpointPayloadCodec codec) => new(codec.ReadText(r), CheckpointTransformState.Read(r),
            new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), r.ReadSingle(), r.ReadDouble());
    }
}

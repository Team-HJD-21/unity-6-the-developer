// 장벽·비콘의 논리 ID와 실제 HP·수명·형상을 보존합니다.
// 복원 초기화는 피해·퇴역을 재실행하지 않고 검색/충돌 표현을 실제 값에 연결합니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

public sealed partial class PlayerFieldDevice
{
    public EntityId EntityId => GetComponent<SceneEntityIdentity>()?.EntityId ?? default;
    internal CheckpointState CaptureCheckpoint()
    {
        if (!IsAlive || EntityId.IsEmpty) throw new InvalidOperationException("Capture a live identified Player field device.");
        return new CheckpointState(EntityId, _kind, _start, _end, _width, _maxHealth, _health, _remaining, _radius, _hitFlash, _scanner.CaptureCheckpoint(),
            CheckpointTransformState.Capture(transform));
    }
    internal void RestoreCheckpoint(CheckpointState state, Transform owner = null)
    {
        if (EntityId != state.Id) throw new InvalidOperationException("The restored field device has a different logical ID.");
        Initialize(state.Kind, state.Start, state.End, state.Width, state.MaximumHealth, state.Remaining, state.Radius, owner);
        _health = state.Health; _remaining = state.Remaining; _hitFlash = state.HitFlash;
        _scanner.RestoreCheckpoint(state.ScanRemaining);
        state.Pose.Apply(transform);
        _visual.Refresh(_kind, _health / _maxHealth, _hitFlash > 0, _radius);
    }
    internal sealed class CheckpointState
    {
        public readonly EntityId Id;
        public readonly PlayerFieldDeviceKind Kind;
        public readonly Vector3 Start, End;
        public readonly float Width, MaximumHealth, Health, Remaining, Radius, HitFlash, ScanRemaining;
        public readonly CheckpointTransformState Pose;
        public CheckpointState(EntityId id, PlayerFieldDeviceKind kind, Vector3 start, Vector3 end,
            float width, float maximumHealth, float health, float remaining, float radius, float hitFlash, float scanRemaining, CheckpointTransformState pose)
        {
            if (id.IsEmpty || id.Value.Length > 128 || !Enum.IsDefined(typeof(PlayerFieldDeviceKind), kind) ||
                kind == PlayerFieldDeviceKind.PlayerTurret || !Valid(width) || width < .1f || !Valid(maximumHealth) || maximumHealth < 1 ||
                !Valid(health) || health <= 0 || health > maximumHealth || !Valid(remaining) || remaining <= 0 ||
                !Valid(radius) || !Valid(hitFlash) || !Valid(scanRemaining) || scanRemaining > .2f) throw new InvalidDataException("Invalid saved Player field device.");
            Id = id; Kind = kind; Start = start; End = end; Width = width; MaximumHealth = maximumHealth;
            Health = health; Remaining = remaining; Radius = radius; HitFlash = hitFlash; ScanRemaining = scanRemaining; Pose = pose;
        }
        public void Write(BinaryWriter writer, ICheckpointPayloadCodec codec)
        {
            codec.WriteText(writer, Id.Value, 128); writer.Write((int)Kind);
            PlayerSkillCheckpointIO.Write(writer, Start); PlayerSkillCheckpointIO.Write(writer, End);
            writer.Write(Width); writer.Write(MaximumHealth); writer.Write(Health); writer.Write(Remaining); writer.Write(Radius); writer.Write(HitFlash); writer.Write(ScanRemaining); Pose.Write(writer);
        }
        public static CheckpointState Read(BinaryReader reader, ICheckpointPayloadCodec codec) => new(new EntityId(codec.ReadText(reader, 128)),
            (PlayerFieldDeviceKind)reader.ReadInt32(), PlayerSkillCheckpointIO.ReadVector(reader), PlayerSkillCheckpointIO.ReadVector(reader),
            reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), CheckpointTransformState.Read(reader));
        private static bool Valid(float value) => PlayerSkillCheckpointIO.Finite(value) && value >= 0;
    }
}

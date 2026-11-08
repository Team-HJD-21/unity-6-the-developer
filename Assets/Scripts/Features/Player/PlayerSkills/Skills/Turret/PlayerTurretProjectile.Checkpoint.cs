// 임시 포탑 발사 후에도 스킬이 소유하는 탄환의 피해·물리·남은 수명을 보존합니다.
using System;
using System.IO;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public sealed partial class PlayerTurretProjectile
{
    internal event Action<PlayerTurretProjectile> Retired;
    internal bool IsCheckpointAlive => this != null && _initialized && !_hit && isActiveAndEnabled && _remaining > 0;
    private void OnDestroy() { Retired?.Invoke(this); Retired = null; }
    internal State CaptureCheckpoint()
    {
        if (!IsCheckpointAlive) throw new InvalidOperationException("Capture a live owned temporary turret shot.");
        var field = GetComponent<FieldSimulationActor>(); var renderer = GetComponent<SpriteRenderer>();
        return new State(_damage, _remaining, renderer != null ? renderer.color : Color.white, CheckpointTransformState.Capture(transform),
            field != null ? field.CaptureBody(_body) : CheckpointRigidbody2DState.Capture(_body));
    }
    internal void RestoreCheckpoint(State state)
    {
        _damage = state.Damage; _remaining = state.Remaining; _initialized = true; _hit = false;
        state.Pose.Apply(transform); var field = GetComponent<FieldSimulationActor>();
        if (field != null) field.RestoreBody(_body, state.Body); else state.Body.Apply(_body);
        var renderer = GetComponent<SpriteRenderer>(); if (renderer != null) renderer.color = state.Color;
    }
    internal sealed class State
    {
        public readonly float Damage, Remaining;
        public readonly Color Color;
        public readonly CheckpointTransformState Pose;
        public readonly CheckpointRigidbody2DState Body;
        public State(float damage, float remaining, Color color, CheckpointTransformState pose, CheckpointRigidbody2DState body)
        {
            if (!PlayerSkillCheckpointIO.Finite(damage) || damage < 0 || !PlayerSkillCheckpointIO.Finite(remaining) || remaining <= 0 ||
                !PlayerSkillCheckpointIO.Finite(color.r) || !PlayerSkillCheckpointIO.Finite(color.g) ||
                !PlayerSkillCheckpointIO.Finite(color.b) || !PlayerSkillCheckpointIO.Finite(color.a)) throw new InvalidDataException("Invalid saved temporary turret shot.");
            Damage = damage; Remaining = remaining; Color = color; Pose = pose; Body = body;
        }
        public void Write(BinaryWriter writer)
        { writer.Write(Damage); writer.Write(Remaining); writer.Write(Color.r); writer.Write(Color.g); writer.Write(Color.b); writer.Write(Color.a); Pose.Write(writer); Body.Write(writer); }
        public static State Read(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(),
            new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), CheckpointTransformState.Read(reader), CheckpointRigidbody2DState.Read(reader));
    }
}

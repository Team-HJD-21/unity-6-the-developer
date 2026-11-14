// 임시 포탑의 실제 내구도·배치·헤드 방향·발사 주기 진행을 보존합니다.
// 매 프레임 재평가하는 적 탐색 캐시는 저장하지 않으며 현재 생성자의 prefab을 사용합니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

public partial class PlayerTurret
{
    internal GameObject CheckpointProjectilePrefab => bulletPrefab;
    internal State CaptureCheckpoint()
    {
        var health = _health.CaptureCheckpoint();
        return new State(_health.EntityId, SlotIndex, health.maximum, health.health, health.hit, health.scan, fireTimer,
            CheckpointTransformState.Capture(transform), CheckpointTransformState.Capture(turretHead != null ? turretHead : transform));
    }
    internal void RestoreCheckpoint(State state)
    {
        if (_health.EntityId != state.Id) throw new InvalidOperationException("The temporary turret identity differs.");
        state.Pose.Apply(transform); state.Head.Apply(turretHead != null ? turretHead : transform);
        _health.RestoreCheckpoint(state.MaximumHealth, state.Health, state.HitFlash, state.ScanRemaining);
        SlotIndex = state.SlotIndex;
        fireTimer = state.FireElapsed; targetEnemy = null; _recovered = false;
    }
    internal sealed class State
    {
        public readonly EntityId Id;
        public readonly int SlotIndex;
        public readonly float MaximumHealth, Health, HitFlash, ScanRemaining, FireElapsed;
        public readonly CheckpointTransformState Pose, Head;
        public State(EntityId id, int slotIndex, float maximum, float health, float hit, float scan, float fireElapsed, CheckpointTransformState pose, CheckpointTransformState head)
        {
            if (id.IsEmpty || id.Value.Length > 128 || slotIndex < 0 || !PlayerSkillCheckpointIO.Finite(maximum) || maximum < 1 ||
                !PlayerSkillCheckpointIO.Finite(health) || health <= 0 || health > maximum || !PlayerSkillCheckpointIO.Finite(hit) || hit < 0 ||
                !PlayerSkillCheckpointIO.Finite(scan) || scan < 0 || scan > .2f || !PlayerSkillCheckpointIO.Finite(fireElapsed) || fireElapsed < 0) throw new InvalidDataException("Invalid saved temporary turret.");
            Id = id; SlotIndex = slotIndex; MaximumHealth = maximum; Health = health; HitFlash = hit; ScanRemaining = scan; FireElapsed = fireElapsed; Pose = pose; Head = head;
        }
        public void Write(BinaryWriter writer, ICheckpointPayloadCodec codec)
        { codec.WriteText(writer, Id.Value, 128); writer.Write(SlotIndex); writer.Write(MaximumHealth); writer.Write(Health); writer.Write(HitFlash); writer.Write(ScanRemaining); writer.Write(FireElapsed); Pose.Write(writer); Head.Write(writer); }
        public static State Read(BinaryReader reader, ICheckpointPayloadCodec codec) => new(new EntityId(codec.ReadText(reader, 128)),
            reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), CheckpointTransformState.Read(reader), CheckpointTransformState.Read(reader));
    }
}

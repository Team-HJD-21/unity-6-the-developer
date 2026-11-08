// CU의 실제 체력과 배치 상태를 체크포인트에 연결합니다.
// 피해·파괴 사건을 재발행하지 않고 검증한 복원값과 HP 표시만 적용합니다.
using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class ControlUnitHealth : ICheckpointParticipant
{
    string ICheckpointParticipant.Key => "world/control-unit";
    int ICheckpointParticipant.Version => 1;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
    int ICheckpointParticipant.RestoreOrder => 100;

    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointSource();
        var pose = CheckpointTransformState.Capture(transform);
        int health = _currentHealth;
        return codec.Write("world/control-unit", 1, CheckpointSectionKind.World, writer =>
        {
            codec.WriteText(writer, _definition.Id);
            writer.Write(MaxHealth); writer.Write(health); pose.Write(writer);
        });
    }

    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context,
        CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointSource();
        var target = codec.Read(section, 1, reader =>
        {
            string definition = codec.ReadText(reader);
            int maxHealth = reader.ReadInt32(), health = reader.ReadInt32();
            var pose = CheckpointTransformState.Read(reader);
            if (definition != _definition.Id || maxHealth != MaxHealth || health <= 0 || health > maxHealth)
                throw new InvalidDataException("The saved CU definition or health does not match this Scene.");
            return (health, pose);
        });
        int previousHealth = _currentHealth;
        var previousPose = CheckpointTransformState.Capture(transform);
        return new CheckpointRestoreStep(() => ApplyCheckpoint(target.health, target.pose),
            () => ApplyCheckpoint(previousHealth, previousPose));
    }

    private void RequireCheckpointSource()
    {
        if (!isActiveAndEnabled || _definition == null || MaxHealth <= 0 || IsDestroyed)
            throw new InvalidOperationException("Capture and restore a configured living CU in an active Match.");
    }

    private void ApplyCheckpoint(int health, CheckpointTransformState pose)
    {
        float previousRatio = MaxHealth > 0 ? _currentHealth / (float)MaxHealth : 0f;
        pose.Apply(transform);
        _currentHealth = health;
        onCUHpChange.Invoke(health, MaxHealth, previousRatio);
        HealthChanged?.Invoke(health, MaxHealth);
    }
}

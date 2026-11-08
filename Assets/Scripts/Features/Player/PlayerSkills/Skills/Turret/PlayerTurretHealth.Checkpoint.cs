// 실제 임시 포탑의 논리 ID·체력·피격 표현의 남은 시간을 저장·복원에 제공합니다.
using System;
using System.IO;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class PlayerTurretHealth
{
    public EntityId EntityId => GetComponent<SceneEntityIdentity>()?.EntityId ?? default;
    internal (float maximum, float health, float hit, float scan) CaptureCheckpoint()
    {
        if (!IsAlive || EntityId.IsEmpty) throw new InvalidOperationException("Capture a live identified temporary turret.");
        return (MaxHealth, _health, _hitRemaining, _scanner.CaptureCheckpoint());
    }
    internal void RestoreCheckpoint(float maximum, float health, float hit, float scan)
    {
        if (maximum != MaxHealth || !PlayerSkillCheckpointIO.Finite(health) || health <= 0 || health > maximum ||
            !PlayerSkillCheckpointIO.Finite(hit) || hit < 0) throw new InvalidDataException("The saved temporary turret health is invalid.");
        _scanner.RestoreCheckpoint(scan); _health = health; _hitRemaining = hit;
    }
}

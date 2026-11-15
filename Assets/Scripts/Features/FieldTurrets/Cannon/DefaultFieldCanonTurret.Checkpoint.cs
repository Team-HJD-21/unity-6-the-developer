// 캐논의 열 누적·현재 표적·소유 탄환을 공통 터렛 writer에 제공합니다.
// 공통 전력/HP와 무기 상태를 나누고 전체 actor 복원 뒤 표적을 연결합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public abstract partial class DefaultFieldCanonTurret : IFieldTurretCombatCheckpoint
{
    private string _restoredTarget;

    protected override void BindProjectileSimulation(GameObject projectile)
    {
        base.BindProjectileSimulation(projectile);
        var actor = projectile.GetComponent<FieldTurretBullet>() ?? throw new InvalidOperationException("Missing turret projectile component.");
        if (TryGetEntityId(out var identity)) actor.CheckpointOwnerId = identity.Value;
        actor.CheckpointSourceDefinition = Definition.Id;
    }

    private IEnumerable<FieldTurretBullet> ReadCheckpointProjectiles()
    {
        if (!TryGetEntityId(out var identity)) throw new InvalidOperationException("Turret checkpoint requires a logical identity.");
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var actor in root.GetComponentsInChildren<FieldTurretBullet>())
                if (actor.CheckpointOwnerId == identity.Value) yield return actor;
    }

    CheckpointSection IFieldTurretCombatCheckpoint.CaptureCombat(ICheckpointPayloadCodec codec)
    {
        var bullets = ReadCheckpointProjectiles().Where(b => b != null && b.isActiveAndEnabled).ToArray();
        if (bullets.Length > 4096) throw new InvalidOperationException("Too many cannon bullets for one checkpoint.");
        return codec.Write("turret/cannon", 1, CheckpointSectionKind.World, w =>
        {
            w.Write(_fireTime); codec.WriteText(w, CheckpointSceneReferences.Capture(Target));
            w.Write(bullets.Length);
            foreach (var bullet in bullets) bullet.CaptureCheckpoint().Write(w, codec);
        });
    }

    void IFieldTurretCombatCheckpoint.ValidateCombat(CheckpointSection section, ICheckpointPayloadCodec codec) => ReadCombat(section, codec);

    private (float heat, string target, FieldTurretBullet.State[] bullets) ReadCombat(CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        if (section.Key != "turret/cannon") throw new InvalidDataException("Wrong turret combat schema.");
        return codec.Read(section, 1, r =>
        {
            float heat = r.ReadSingle(); string target = codec.ReadText(r);
            if (!float.IsFinite(heat) || heat < 0 || (bulletPrefab == null || bulletPrefab.GetComponent<FieldTurretBullet>() == null))
                throw new InvalidDataException("Invalid cannon heat or bullet prefab.");
            var bullets = new FieldTurretBullet.State[codec.ReadCount(r, 4096)];
            for (int i = 0; i < bullets.Length; i++) bullets[i] = FieldTurretBullet.State.Read(r, codec);
            return (heat, target, bullets);
        });
    }

    void IFieldTurretCombatCheckpoint.ReleaseCombat()
    {
        foreach (var bullet in ReadCheckpointProjectiles().ToArray()) if (bullet != null) CheckpointActorFactory.Retire(bullet.gameObject);
        Target = null;
    }

    void IFieldTurretCombatCheckpoint.RestoreCombat(CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        var state = ReadCombat(section, codec); _fireTime = state.heat; _restoredTarget = state.target;
        foreach (var saved in state.bullets)
        {
            var source = CheckpointOwner.ResolveCheckpointPrefab(saved.SourceDefinition) as DefaultFieldCanonTurret;
            if (source == null || source.bulletPrefab == null) throw new InvalidDataException("Missing original projectile prefab.");
            var created = Instantiate(source.bulletPrefab, saved.Pose.Position, saved.Pose.Rotation);
            BindProjectileSimulation(created);
            created.GetComponent<FieldTurretBullet>().RestoreCheckpoint(saved, transform);
        }
    }

    void IFieldTurretCombatCheckpoint.ResolveCombatReferences()
    {
        Target = CheckpointSceneReferences.Resolve(gameObject.scene, _restoredTarget);
        _restoredTarget = null;
        // 복원도 실제 과열 표현을 사용하며, 식은 OFF 터렛의 기존 회색을 유지합니다.
        SetGunHeatTint(OverHeatTime > 0f ? _fireTime / OverHeatTime : 0f);
        if (!IsActivated && _fireTime <= 0f && GunRenderer != null)
            GunRenderer.color = Color.gray;
    }
}

// 미사일 터렛의 열·미발사 claim·발사된 미사일을 한 전투 원본으로 저장합니다.
// 공통 Controller가 actor를 교체한 뒤 타깃을 논리 ID로 연결합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public abstract partial class DefaultFieldMissileTurret : IFieldTurretCombatCheckpoint
{
    private readonly List<(FieldTurretMissile actor, FieldTurretMissile.State state)> _restoredMissiles = new();
    private string[] _restoredTargets;

    protected override void BindProjectileSimulation(GameObject projectile)
    {
        base.BindProjectileSimulation(projectile);
        var actor = projectile.GetComponent<FieldTurretMissile>() ?? throw new InvalidOperationException("Missing turret projectile component.");
        if (TryGetEntityId(out var identity)) actor.CheckpointOwnerId = identity.Value;
        actor.CheckpointSourceDefinition = Definition.Id;
    }

    private IEnumerable<FieldTurretMissile> ReadCheckpointProjectiles()
    {
        if (!TryGetEntityId(out var identity)) throw new InvalidOperationException("Turret checkpoint requires a logical identity.");
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var actor in root.GetComponentsInChildren<FieldTurretMissile>())
                if (actor.CheckpointOwnerId == identity.Value) yield return actor;
    }

    CheckpointSection IFieldTurretCombatCheckpoint.CaptureCombat(ICheckpointPayloadCodec codec)
    {
        var missiles = ReadCheckpointProjectiles().Where(m => m != null && m.isActiveAndEnabled).ToArray();
        if (missiles.Length > 4096) throw new InvalidOperationException("Too many missiles for one checkpoint.");
        return codec.Write("turret/missile", 1, CheckpointSectionKind.World, w =>
        {
            w.Write(_currentMissileCount); w.Write(Targets?.Length ?? 0);
            foreach (var target in Targets ?? Array.Empty<Transform>()) codec.WriteText(w, CheckpointSceneReferences.Capture(target));
            w.Write(missiles.Length);
            foreach (var missile in missiles) missile.CaptureCheckpoint().Write(w, codec);
        });
    }

    void IFieldTurretCombatCheckpoint.ValidateCombat(CheckpointSection section, ICheckpointPayloadCodec codec) => ReadCombat(section, codec);
    private (float heat, string[] targets, FieldTurretMissile.State[] missiles) ReadCombat(CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        if (section.Key != "turret/missile") throw new InvalidDataException("Wrong turret combat schema.");
        return codec.Read(section, 1, r =>
        {
            float heat = r.ReadSingle(); var targets = new string[codec.ReadCount(r, 16)];
            for (int i = 0; i < targets.Length; i++) targets[i] = codec.ReadText(r);
            if (targets.Length != ProjectilesPerShot || !float.IsFinite(heat) || heat < 0 || missilePrefab == null || missilePrefab.GetComponent<FieldTurretMissile>() == null)
                throw new InvalidDataException("Invalid missile heat or prefab.");
            var missiles = new FieldTurretMissile.State[codec.ReadCount(r, 4096)];
            for (int i = 0; i < missiles.Length; i++) missiles[i] = FieldTurretMissile.State.Read(r, codec);
            return (heat, targets, missiles);
        });
    }

    void IFieldTurretCombatCheckpoint.ReleaseCombat()
    {
        ReleaseUnlaunchedTargets();
        foreach (var missile in ReadCheckpointProjectiles().ToArray()) if (missile != null) CheckpointActorFactory.Retire(missile.gameObject);
        _restoredMissiles.Clear();
    }

    void IFieldTurretCombatCheckpoint.RestoreCombat(CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        var state = ReadCombat(section, codec); _currentMissileCount = state.heat; _restoredTargets = state.targets;
        Targets = new Transform[state.targets.Length];
        foreach (var saved in state.missiles)
        {
            var source = CheckpointOwner.ResolveCheckpointPrefab(saved.SourceDefinition) as DefaultFieldMissileTurret;
            if (source == null || source.missilePrefab == null) throw new InvalidDataException("Missing original projectile prefab.");
            var created = Instantiate(source.missilePrefab, saved.Pose.Position, saved.Pose.Rotation);
            BindProjectileSimulation(created);
            var missile = created.GetComponent<FieldTurretMissile>(); missile.RestoreCheckpoint(saved);
            _restoredMissiles.Add((missile, saved));
        }
    }

    void IFieldTurretCombatCheckpoint.ResolveCombatReferences()
    {
        if (Targets == null || Targets.Length != _restoredTargets.Length)
            throw new InvalidDataException("Saved missile target slots differ from the current turret.");
        _targetReservations = new MissileTargetReservation[Targets.Length];
        for (int i = 0; i < Targets.Length; i++)
        {
            Targets[i] = CheckpointSceneReferences.Resolve(gameObject.scene, _restoredTargets[i]);
            if (Targets[i] != null) _targetReservations[i] = MissileTargetReservation.RestoreCheckpointClaim(Targets[i]);
        }
        foreach (var pair in _restoredMissiles) pair.actor.ResolveCheckpointTarget(pair.state);
        _restoredMissiles.Clear(); _restoredTargets = null;
        float ratio = _currentMissileCount / OverHeatMissileCount;
        GunRenderer.color = new Color(1, 1 - ratio, 1 - ratio);
    }
}

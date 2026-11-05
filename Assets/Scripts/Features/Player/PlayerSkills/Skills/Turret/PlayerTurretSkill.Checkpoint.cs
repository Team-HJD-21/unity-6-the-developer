// 임시 포탑 스킬의 수량·모든 수리 큐·설치 미리보기·실제 포탑·비행 중 탄환을 복원합니다.
// 교체 정리는 Owner를 먼저 해제해 파괴 통지가 수량을 다시 변경하지 않도록 합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public sealed partial class PlayerTurretSkill
{
    private readonly HashSet<PlayerTurretProjectile> _projectiles = new();
    internal void TrackProjectile(PlayerTurretProjectile projectile) { _projectiles.Add(projectile); projectile.Retired += OnProjectileRetired; }
    private void OnProjectileRetired(PlayerTurretProjectile projectile) { _projectiles.Remove(projectile); projectile.Retired -= OnProjectileRetired; }
    private void RemoveProjectiles()
    {
        var previous = _projectiles.ToArray(); _projectiles.Clear();
        foreach (var projectile in previous) if (projectile != null) { projectile.Retired -= OnProjectileRetired; CheckpointActorFactory.Retire(projectile.gameObject); }
    }
    protected override Action<BinaryWriter> CaptureCheckpointState(ICheckpointPayloadCodec codec)
    {
        Saved state = CaptureTurrets();
        return writer =>
        {
            PlayerSkillCheckpointIO.Write(writer, state.Inventory); writer.Write(state.Cooldown); writer.Write(state.Placing);
            writer.Write((int)state.Feedback); state.PreviewPose.Write(writer); writer.Write(state.PreviewVisible);
            writer.Write(state.Turrets.Length); foreach (var turret in state.Turrets) turret.Write(writer, codec);
            writer.Write(state.Projectiles.Length); foreach (var projectile in state.Projectiles) projectile.Write(writer);
        };
    }
    protected override ICheckpointRestoreStep PrepareCheckpointState(BinaryReader reader, ICheckpointPayloadCodec codec)
    {
        var inventory = PlayerSkillCheckpointIO.ReadInventory(reader, codec); float cooldown = PlayerSkillCheckpointIO.ReadDuration(reader);
        bool placing = reader.ReadBoolean(); var feedback = (PlayerTurretPlacementResult)reader.ReadInt32();
        var previewPose = CheckpointTransformState.Read(reader); bool visible = reader.ReadBoolean();
        var turrets = new PlayerTurret.State[codec.ReadCount(reader, PlayerSkillCheckpointIO.MaximumOwnedActors)];
        var ids = new HashSet<TeamHJD.Game.Domain.EntityId>();
        for (int i = 0; i < turrets.Length; i++) { turrets[i] = PlayerTurret.State.Read(reader, codec); if (!ids.Add(turrets[i].Id)) throw new InvalidDataException("Duplicate temporary turret identity."); }
        var projectiles = new PlayerTurretProjectile.State[codec.ReadCount(reader, PlayerSkillCheckpointIO.MaximumOwnedActors)];
        for (int i = 0; i < projectiles.Length; i++) projectiles[i] = PlayerTurretProjectile.State.Read(reader);
        var template = turretPrefab != null ? turretPrefab.GetComponent<PlayerTurret>() : null;
        if (inventory.Capacity != MaxTurretCount || inventory.MaximumRepairSeconds != _inventory.MaximumRepairSeconds || inventory.Deployed != turrets.Length ||
            !Enum.IsDefined(typeof(PlayerTurretPlacementResult), feedback) || (placing && (_previewPrefab == null || !_previewPrefab.IsConfigured)) ||
            ((turrets.Length > 0 || projectiles.Length > 0) && template == null) ||
            (projectiles.Length > 0 && (template.CheckpointProjectilePrefab == null || template.CheckpointProjectilePrefab.GetComponent<PlayerTurretProjectile>() == null)))
            throw new InvalidDataException("The saved temporary turret definition or inventory differs.");
        if (turrets.Length > 0 && turrets.Any(t => t.MaximumHealth != template.GetComponent<PlayerTurretHealth>().MaxHealth))
            throw new InvalidDataException("The temporary turret HP definition differs.");
        foreach (var turret in turrets) CheckpointActorFactory.ValidatePrefab(turretPrefab, turret.Id);
        var target = new Saved(inventory, cooldown, placing, feedback, previewPose, visible, turrets, projectiles); Saved backup = CaptureTurrets();
        return new CheckpointRestoreStep(() => Apply(target), () => Apply(backup));
    }
    private Saved CaptureTurrets()
    {
        foreach (var projectile in _projectiles.Where(p => p == null || !p.IsCheckpointAlive).ToArray())
        { _projectiles.Remove(projectile); if (projectile != null) projectile.Retired -= OnProjectileRetired; }
        var turrets = _deployed.Where(t => t != null && t.Health != null && t.Health.IsAlive).Select(t => t.CaptureCheckpoint()).ToArray();
        var inventory = _inventory.CaptureState();
        if (turrets.Length > PlayerSkillCheckpointIO.MaximumOwnedActors || _projectiles.Count > PlayerSkillCheckpointIO.MaximumOwnedActors || inventory.Deployed != turrets.Length)
            throw new InvalidOperationException("The owned turret actors and inventory are not at a consistent capture boundary.");
        return new Saved(inventory, _installationRemaining, IsPlacing, _placementFeedback,
            CheckpointTransformState.Capture(IsPlacing ? _preview.transform : transform), IsPlacing && _preview.gameObject.activeSelf,
            turrets, _projectiles.Select(p => p.CaptureCheckpoint()).ToArray());
    }
    private void Apply(Saved state)
    {
        CancelPlacement(); RemoveProjectiles();
        foreach (var turret in _deployed)
            if (turret != null) { turret.AssignOwner(null); CheckpointActorFactory.Retire(turret.gameObject); }
        _deployed.Clear(); _inventory.RestoreState(state.Inventory); _installationRemaining = state.Cooldown;
        foreach (var turret in state.Turrets)
        {
            var actor = CheckpointActorFactory.Instantiate(turretPrefab, gameObject.scene, turret.Pose, turret.Id);
            try { var source = actor.GetComponent<PlayerTurret>(); source.RestoreCheckpoint(turret); FieldSimulationTime.BindSpawned(actor, this); source.AssignOwner(this); _deployed.Add(source); }
            catch { CheckpointActorFactory.Retire(actor); throw; }
        }
        if (state.Projectiles.Length > 0)
        {
            var prefab = turretPrefab.GetComponent<PlayerTurret>().CheckpointProjectilePrefab;
            foreach (var projectile in state.Projectiles)
            {
                var actor = UnityEngine.Object.Instantiate(prefab, projectile.Pose.Position, projectile.Pose.Rotation);
                try { var source = actor.GetComponent<PlayerTurretProjectile>(); source.RestoreCheckpoint(projectile); FieldSimulationTime.BindSpawned(actor, this); TrackProjectile(source); }
                catch { CheckpointActorFactory.Retire(actor); throw; }
            }
        }
        _placementFeedback = state.Feedback;
        if (state.Placing)
        {
            _preview = UnityEngine.Object.Instantiate(_previewPrefab, state.PreviewPose.Position, state.PreviewPose.Rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_preview.gameObject, gameObject.scene);
            state.PreviewPose.Apply(_preview.transform); _preview.SetVisible(state.PreviewVisible);
            // 복원 버튼의 입력을 설치 명령으로 재사용하지 않고 다음 프레임의 새 입력부터 받습니다.
            _placementStartFrame = Time.frameCount; _nextPreviewCheck = 0;
        }
    }
    private sealed class Saved
    {
        public readonly PlayerTurretInventory.State Inventory; public readonly float Cooldown; public readonly bool Placing, PreviewVisible;
        public readonly PlayerTurretPlacementResult Feedback; public readonly CheckpointTransformState PreviewPose;
        public readonly PlayerTurret.State[] Turrets; public readonly PlayerTurretProjectile.State[] Projectiles;
        public Saved(PlayerTurretInventory.State inventory, float cooldown, bool placing, PlayerTurretPlacementResult feedback,
            CheckpointTransformState pose, bool visible, PlayerTurret.State[] turrets, PlayerTurretProjectile.State[] projectiles)
        { Inventory = inventory; Cooldown = cooldown; Placing = placing; Feedback = feedback; PreviewPose = pose; PreviewVisible = visible; Turrets = turrets; Projectiles = projectiles; }
    }
}

// Player 무기 선택·재사용 시간·진행 중 렌치와 실제 소유 탄환을 체크포인트로 연결합니다.
// 필드 밖 전역 검색이나 Clear-only 복원 대신 발사자가 관리한 살아 있는 탄환을 다시 구성합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public partial class PlayerAttack : ICheckpointParticipant
{
    private const int MaximumSavedProjectiles = 4096;
    private readonly HashSet<PlayerBullet> _projectiles = new();
    private PlayerActorIdentity CheckpointIdentity => GetComponent<PlayerActorIdentity>() ?? throw new InvalidOperationException("Bind the Player actor before weapon checkpoints.");
    string ICheckpointParticipant.Key => CheckpointIdentity.CheckpointKeyPrefix + "/weapon";
    int ICheckpointParticipant.Version => 2;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
    int ICheckpointParticipant.RestoreOrder => 220;

    private void TrackProjectile(PlayerBullet bullet) { _projectiles.Add(bullet); bullet.Retired += OnProjectileRetired; }
    private void OnProjectileRetired(PlayerBullet bullet) { _projectiles.Remove(bullet); bullet.Retired -= OnProjectileRetired; }
    private void OnDestroy() => RemoveProjectiles();
    private void RemoveProjectiles()
    {
        var previous = _projectiles.ToArray(); _projectiles.Clear();
        foreach (var bullet in previous)
        {
            if (bullet == null) continue;
            bullet.Retired -= OnProjectileRetired;
            bullet.gameObject.SetActive(false);
            if (UnityEngine.Application.isPlaying) Destroy(bullet.gameObject); else DestroyImmediate(bullet.gameObject);
        }
    }

    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context); SavedWeapon state = CaptureWeapon();
        return codec.Write(((ICheckpointParticipant)this).Key, 2, CheckpointSectionKind.World, writer =>
        {
            codec.WriteText(writer, CheckpointIdentity.PlayerId.Value);
            writer.Write((int)state.Weapon); writer.Write(state.ShotRemaining);
            writer.Write(state.Spanner != null); state.Spanner?.Write(writer);
            writer.Write(state.Projectiles.Length);
            foreach (var bullet in state.Projectiles) bullet.Write(writer);
            writer.Write(state.Ammo != null); state.Ammo?.Write(writer);
        });
    }

    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context.Current);
        if (section.Version != 1 && section.Version != 2)
            throw new NotSupportedException("Unsupported Player weapon checkpoint version: " + section.Version);
        SavedWeapon target = codec.Read(section, section.Version, reader =>
        {
            if (codec.ReadText(reader) != CheckpointIdentity.PlayerId.Value) throw new InvalidDataException("The weapon belongs to another logical Player.");
            var weapon = (WeaponType)reader.ReadInt32(); double remaining = reader.ReadDouble();
            var spanner = reader.ReadBoolean() ? PlayerSpannerWeapon.CheckpointState.Read(reader) : null;
            int count = codec.ReadCount(reader, MaximumSavedProjectiles);
            var projectiles = new PlayerBullet.CheckpointState[count];
            for (int i = 0; i < count; i++) projectiles[i] = PlayerBullet.CheckpointState.Read(reader);
            var ammo = section.Version >= 2 && reader.ReadBoolean()
                ? PlayerWeaponAmmoState.CheckpointState.Read(reader) : null;
            if (!_selection.IsWeaponAvailable(weapon) || !Enum.IsDefined(typeof(WeaponType), weapon) || double.IsNaN(remaining) || double.IsInfinity(remaining) || remaining < 0 ||
                (spanner != null) != (_selection.Spanner != null) || (spanner?.Pending == true && weapon != WeaponType.Spanner) ||
                (weapon == WeaponType.Spanner && spanner == null)) throw new InvalidDataException("The saved weapon state does not match this Player configuration.");
            if (projectiles.Any(saved => !HasCheckpointPrefab(saved.SourceWeapon)))
                throw new InvalidDataException("The saved Player projectiles require the configured bullet prefab.");
            if (ammo != null)
            {
                if (_ammo == null) throw new InvalidDataException("Saved magazines require PlayerWeaponAmmo.");
                _ammo.ValidateCheckpoint(ammo, weapon);
            }
            return new SavedWeapon(weapon, remaining, spanner, projectiles, ammo);
        });
        SavedWeapon backup = CaptureWeapon();
        return new CheckpointRestoreStep(() => ApplyWeapon(target), () => ApplyWeapon(backup));
    }

    private void RequireCheckpointPlayer(CheckpointCaptureContext context)
    {
        var identity = CheckpointIdentity;
        if (context == null || identity.SlotIndex < 0 || identity.SlotIndex >= context.Config.Players.Count ||
            context.Config.Players[identity.SlotIndex].PlayerId != identity.PlayerId)
            throw new InvalidOperationException("Use the weapon writer only in its current Player roster.");
        if (Presence?.TimeSource != null && (Presence.TimeSource.Snapshot.MatchId != context.Config.MatchId || !Presence.CanReport))
            throw new InvalidOperationException("Only the current Host weapon can provide checkpoint state.");
    }

    private SavedWeapon CaptureWeapon()
    {
        foreach (var retired in _projectiles.Where(bullet => bullet == null || !bullet.IsCheckpointAlive).ToArray())
        {
            _projectiles.Remove(retired);
            if (retired != null) retired.Retired -= OnProjectileRetired;
        }
        if (_projectiles.Count > MaximumSavedProjectiles) throw new InvalidOperationException("The owned projectile snapshot exceeds its storage limit.");
        return new SavedWeapon(_selection.CurrentWeapon, Math.Max(0, _nextShotTime - GameSeconds), _selection.Spanner != null ? _selection.Spanner.CaptureCheckpoint() : null,
            _projectiles.Select(bullet => bullet.CaptureCheckpoint()).ToArray(), _ammo != null ? _ammo.CaptureCheckpoint() : null);
    }

    private void ApplyWeapon(SavedWeapon state)
    {
        RemoveProjectiles();
        _selection.RestoreWeapon(state.Weapon);
        // 무기 변경 알림의 재장전 취소 뒤에 탄창과 남은 재장전 시간을 적용합니다.
        if (_ammo != null) _ammo.RestoreCheckpoint(state.Ammo);
        _nextShotTime = GameSeconds + state.ShotRemaining;
        if (_selection.Spanner != null) _selection.Spanner.RestoreCheckpoint(state.Spanner);
        foreach (var saved in state.Projectiles)
        {
            GameObject actor = Instantiate(_selection.ResolveBulletPrefab(saved.SourceWeapon), saved.Pose.Position, saved.Pose.Rotation);
            try
            {
                FieldSimulationTime.BindSpawned(actor, this);
                var bullet = actor.GetComponent<PlayerBullet>(); bullet.RestoreCheckpoint(saved); TrackProjectile(bullet);
            }
            catch { actor.SetActive(false); if (UnityEngine.Application.isPlaying) Destroy(actor); else DestroyImmediate(actor); throw; }
        }
    }

    private bool HasCheckpointPrefab(WeaponType weapon)
    {
        var prefab = _selection.ResolveBulletPrefab(weapon);
        return weapon != WeaponType.Spanner && _selection.IsWeaponAvailable(weapon) && prefab != null &&
            prefab.GetComponent<PlayerBullet>() != null && prefab.GetComponent<Rigidbody2D>() != null &&
            prefab.GetComponentsInChildren<Collider2D>(true).Any();
    }

    private sealed class SavedWeapon
    {
        public readonly WeaponType Weapon;
        public readonly double ShotRemaining;
        public readonly PlayerSpannerWeapon.CheckpointState Spanner;
        public readonly PlayerBullet.CheckpointState[] Projectiles;
        public readonly PlayerWeaponAmmoState.CheckpointState Ammo;
        public SavedWeapon(WeaponType weapon, double remaining, PlayerSpannerWeapon.CheckpointState spanner,
            PlayerBullet.CheckpointState[] projectiles, PlayerWeaponAmmoState.CheckpointState ammo)
        { Weapon = weapon; ShotRemaining = remaining; Spanner = spanner; Projectiles = projectiles; Ammo = ammo; }
    }
}

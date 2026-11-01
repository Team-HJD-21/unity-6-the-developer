// 폭탄 스킬의 실제 충전·재사용 시간과 발사자가 소유한 시한폭탄들을 저장·복원합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class PlayerBomb
{
    private readonly HashSet<PlayerBombExplosion> _bombs = new();
    private void TrackBomb(UnityEngine.GameObject actor)
    {
        var bomb = actor.GetComponent<PlayerBombExplosion>();
        if (bomb == null) { CheckpointActorFactory.Retire(actor); throw new InvalidOperationException("The authored bomb needs PlayerBombExplosion."); }
        _bombs.Add(bomb); bomb.Retired += OnBombRetired;
    }
    private void OnBombRetired(PlayerBombExplosion bomb) { _bombs.Remove(bomb); bomb.Retired -= OnBombRetired; }
    private void RemoveBombs()
    {
        var previous = _bombs.ToArray(); _bombs.Clear();
        foreach (var bomb in previous) if (bomb != null) { bomb.Retired -= OnBombRetired; CheckpointActorFactory.Retire(bomb.gameObject); }
    }
    private void OnDestroy() => RemoveBombs();
    protected override Action<BinaryWriter> CaptureCheckpointState(ICheckpointPayloadCodec codec)
    {
        Saved state = CaptureBombs();
        return writer =>
        {
            PlayerSkillCheckpointIO.Write(writer, state.Charges); writer.Write(state.Cooldown); writer.Write(state.Bombs.Length);
            foreach (var bomb in state.Bombs) bomb.Write(writer, codec);
        };
    }
    protected override ICheckpointRestoreStep PrepareCheckpointState(BinaryReader reader, ICheckpointPayloadCodec codec)
    {
        var charges = PlayerSkillCheckpointIO.ReadCharges(reader); float cooldown = PlayerSkillCheckpointIO.ReadDuration(reader);
        var bombs = new PlayerBombExplosion.State[codec.ReadCount(reader, PlayerSkillCheckpointIO.MaximumOwnedActors)];
        var ids = new HashSet<TeamHJD.Game.Domain.EntityId>();
        for (int i = 0; i < bombs.Length; i++) { bombs[i] = PlayerBombExplosion.State.Read(reader, codec); if (!ids.Add(bombs[i].Id)) throw new InvalidDataException("Duplicate bomb identity."); }
        if (charges.Maximum != MaxBombCount || charges.RechargeSeconds != Math.Max(.1f, bombRechargeTime) ||
            (bombs.Length > 0 && (bombPrefab == null || bombPrefab.GetComponent<PlayerBombExplosion>() == null)))
            throw new InvalidDataException("The saved bomb definition differs from this Player.");
        foreach (var bomb in bombs) CheckpointActorFactory.ValidatePrefab(bombPrefab, bomb.Id);
        var target = new Saved(charges, cooldown, bombs); Saved backup = CaptureBombs();
        return new CheckpointRestoreStep(() => Apply(target), () => Apply(backup));
    }
    private Saved CaptureBombs()
    {
        foreach (var bomb in _bombs.Where(b => b == null || !b.IsCheckpointAlive).ToArray())
        { _bombs.Remove(bomb); if (bomb != null) bomb.Retired -= OnBombRetired; }
        if (_bombs.Count > PlayerSkillCheckpointIO.MaximumOwnedActors) throw new InvalidOperationException("The owned bomb snapshot exceeds its limit.");
        return new Saved(_charges.CaptureState(), _remainingCooldown, _bombs.Select(b => b.CaptureCheckpoint()).ToArray());
    }
    private void Apply(Saved state)
    {
        RemoveBombs(); _charges.RestoreState(state.Charges); _remainingCooldown = state.Cooldown;
        foreach (var bomb in state.Bombs)
        {
            var actor = CheckpointActorFactory.Instantiate(bombPrefab, gameObject.scene, bomb.Pose, bomb.Id);
            try { actor.GetComponent<PlayerBombExplosion>().RestoreCheckpoint(bomb); PlayerFieldSimulationUtility.BindSpawned(actor, this); TrackBomb(actor); }
            catch { CheckpointActorFactory.Retire(actor); throw; }
        }
    }
    private sealed class Saved
    {
        public readonly PlayerSkillCharges.State Charges; public readonly float Cooldown; public readonly PlayerBombExplosion.State[] Bombs;
        public Saved(PlayerSkillCharges.State charges, float cooldown, PlayerBombExplosion.State[] bombs) { Charges = charges; Cooldown = cooldown; Bombs = bombs; }
    }
}

// PlayerInfo의 실제 생존·체력·회복 진행과 actor 배치/위치를 저장합니다.
// roster 슬롯과 논리 Player ID를 검증하고 사망 사건 재발행이나 UI 잠금 해제 없이 복원합니다.
using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public partial class PlayerInfo : ICheckpointParticipant
{
    private PlayerActorIdentity CheckpointIdentity => GetComponent<PlayerActorIdentity>() ??
        throw new InvalidOperationException("Bind the actual Player actor identity before checkpoint registration.");
    string ICheckpointParticipant.Key => CheckpointIdentity.CheckpointKeyPrefix + "/actor";
    int ICheckpointParticipant.Version => 1;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
    int ICheckpointParticipant.RestoreOrder => 200;

    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context);
        SavedPlayer state = CapturePlayerState();
        return codec.Write(((ICheckpointParticipant)this).Key, 1, CheckpointSectionKind.World, writer =>
        {
            codec.WriteText(writer, CheckpointIdentity.PlayerId.Value);
            writer.Write(state.Health); writer.Write(state.MaximumHealth); writer.Write(state.Dead);
            writer.Write(state.Recovering); writer.Write(state.RecoveryRemaining);
            writer.Write((int)state.Location); writer.Write(state.HasPresence);
            writer.Write(state.CanMove); state.Pose.Write(writer);
            writer.Write(state.Body.HasValue); if (state.Body.HasValue) state.Body.Value.Write(writer);
            writer.Write(state.Dash != null); state.Dash?.Write(writer);
        });
    }

    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context,
        CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context.Current);
        SavedPlayer target = codec.Read(section, 1, reader =>
        {
            if (codec.ReadText(reader) != CheckpointIdentity.PlayerId.Value)
                throw new InvalidDataException("The checkpoint belongs to a different logical Player in this slot.");
            int health = reader.ReadInt32(), maximum = reader.ReadInt32(); bool dead = reader.ReadBoolean();
            bool recovering = reader.ReadBoolean(); double remaining = reader.ReadDouble();
            var location = (MatchPlayerLocation)reader.ReadInt32(); bool presence = reader.ReadBoolean();
            bool canMove = reader.ReadBoolean(); var pose = CheckpointTransformState.Read(reader);
            CheckpointRigidbody2DState? body = reader.ReadBoolean() ? CheckpointRigidbody2DState.Read(reader) : null;
            var dash = reader.ReadBoolean() ? PlayerDash.CheckpointState.Read(reader) : null;
            if (maximum <= 0 || health < 0 || health > maximum || (dead && recovering) ||
                double.IsNaN(remaining) || double.IsInfinity(remaining) || remaining < 0 || remaining > RecoveryInterval ||
                !Enum.IsDefined(typeof(MatchPlayerLocation), location))
                throw new InvalidDataException("Invalid saved Player vitals or location.");
            if (body.HasValue != (GetComponent<UnityEngine.Rigidbody2D>() != null))
                throw new InvalidDataException("The target Player physics configuration differs from the saved actor.");
            if ((dash != null) != (GetComponent<PlayerDash>() != null))
                throw new InvalidDataException("The target Player dash configuration differs from the saved actor.");
            if (presence && GetComponent<PlayerMatchPresence>()?.CanReport != true)
                throw new InvalidOperationException("Bind Host presence before restoring the Player location.");
            return new SavedPlayer(health, maximum, dead, recovering, remaining, location, presence, canMove, pose, body, dash);
        });
        SavedPlayer backup = CapturePlayerState();
        return new CheckpointRestoreStep(() => ApplyPlayerState(target), () => ApplyPlayerState(backup));
    }

    private void RequireCheckpointPlayer(CheckpointCaptureContext context)
    {
        var identity = CheckpointIdentity;
        if (!isActiveAndEnabled || context == null || identity.SlotIndex < 0 || identity.SlotIndex >= context.Config.Players.Count ||
            context.Config.Players[identity.SlotIndex].PlayerId != identity.PlayerId)
            throw new InvalidOperationException("Use the actual Player writer in its configured roster slot.");
        var presence = GetComponent<PlayerMatchPresence>();
        if (presence?.TimeSource != null && (presence.TimeSource.Snapshot.MatchId != context.Config.MatchId || !presence.CanReport))
            throw new InvalidOperationException("Only the current Host Player actor can provide checkpoint state.");
    }

    private SavedPlayer CapturePlayerState()
    {
        if (maxHp <= 0 || curHp < 0 || curHp > maxHp) throw new InvalidOperationException("Player vitals are outside their valid range.");
        var body = GetComponent<UnityEngine.Rigidbody2D>(); var field = GetComponent<FieldSimulationActor>();
        CheckpointRigidbody2DState? physics = body == null ? null : field != null ? field.CaptureBody(body) : CheckpointRigidbody2DState.Capture(body);
        var presence = GetComponent<PlayerMatchPresence>();
        return new SavedPlayer(curHp, maxHp, _isDead, _recovering,
            _recovering ? Math.Min(RecoveryInterval, Math.Max(0, _nextRecoveryAt - FieldSimulationTime.Seconds(this))) : 0,
            presence != null ? presence.Location : MatchPlayerLocation.Field, presence?.TimeSource != null,
            GetComponent<PlayerMovement>()?.CanMove ?? true, CheckpointTransformState.Capture(transform), physics,
            GetComponent<PlayerDash>()?.CaptureCheckpoint());
    }

    private void ApplyPlayerState(SavedPlayer state)
    {
        state.Pose.Apply(transform);
        var body = GetComponent<UnityEngine.Rigidbody2D>(); var field = GetComponent<FieldSimulationActor>();
        if (state.Body.HasValue)
        {
            if (field != null) field.RestoreBody(body, state.Body.Value);
            else state.Body.Value.Apply(body);
        }
        curHp = state.Health; maxHp = state.MaximumHealth; _isDead = state.Dead;
        _recovering = state.Recovering; _nextRecoveryAt = FieldSimulationTime.Seconds(this) + state.RecoveryRemaining;
        GetComponent<PlayerControlState>()?.SetLifeBlocked(state.Dead);
        var movement = GetComponent<PlayerMovement>(); if (movement != null) movement.CanMove = state.CanMove;
        if (state.Dash != null) GetComponent<PlayerDash>().RestoreCheckpoint(state.Dash);
        if (state.HasPresence) GetComponent<PlayerMatchPresence>().Report(state.Location);
        onHpChange.Invoke(curHp, maxHp);
    }

    private sealed class SavedPlayer
    {
        public readonly int Health, MaximumHealth;
        public readonly bool Dead, Recovering, HasPresence, CanMove;
        public readonly double RecoveryRemaining;
        public readonly MatchPlayerLocation Location;
        public readonly CheckpointTransformState Pose;
        public readonly CheckpointRigidbody2DState? Body;
        public readonly PlayerDash.CheckpointState Dash;
        public SavedPlayer(int health, int maximumHealth, bool dead, bool recovering, double remaining,
            MatchPlayerLocation location, bool hasPresence, bool canMove, CheckpointTransformState pose, CheckpointRigidbody2DState? body, PlayerDash.CheckpointState dash)
        {
            Health = health; MaximumHealth = maximumHealth; Dead = dead; Recovering = recovering; RecoveryRemaining = remaining;
            Location = location; HasPresence = hasPresence; CanMove = canMove; Pose = pose; Body = body;
            Dash = dash;
        }
    }
}

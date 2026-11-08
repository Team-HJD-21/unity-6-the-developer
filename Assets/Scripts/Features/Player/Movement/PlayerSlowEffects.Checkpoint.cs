// 감속의 각 배율과 남은 게임 초를 저장합니다. 복원 때문에 피격을 재실행하지 않습니다.
// 새 Match의 시각에 기한을 다시 연결하고 실제 Player 슬롯/Host 원본을 검증합니다.
using System;
using System.IO;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class PlayerSlowEffects : ICheckpointParticipant
{
    private PlayerActorIdentity CheckpointIdentity => GetComponent<PlayerActorIdentity>() ??
        throw new InvalidOperationException("Bind the actual Player before movement effect checkpoints.");
    string ICheckpointParticipant.Key => CheckpointIdentity.CheckpointKeyPrefix + "/movement-effects";
    int ICheckpointParticipant.Version => 1;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
    int ICheckpointParticipant.RestoreOrder => 205;

    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointActor(context);
        var saved = CaptureEffects();
        return codec.Write(((ICheckpointParticipant)this).Key, 1, CheckpointSectionKind.World, writer =>
        {
            codec.WriteText(writer, CheckpointIdentity.PlayerId.Value);
            writer.Write(saved.Length);
            foreach (var effect in saved)
            {
                writer.Write(effect.Multiplier);
                writer.Write(effect.RemainingSeconds);
            }
        });
    }

    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointActor(context.Current);
        var target = codec.Read(section, 1, reader =>
        {
            if (codec.ReadText(reader) != CheckpointIdentity.PlayerId.Value)
                throw new InvalidDataException("Movement effects belong to another Player.");
            var effects = new SavedSlowEffect[codec.ReadCount(reader, MaximumEffects)];
            for (int i = 0; i < effects.Length; i++)
            {
                float multiplier = reader.ReadSingle();
                double remaining = reader.ReadDouble();
                if (!float.IsFinite(multiplier) || multiplier <= 0 || multiplier >= 1 || !double.IsFinite(remaining) || remaining <= 0 || remaining > float.MaxValue)
                    throw new InvalidDataException("Invalid saved Player slow effect.");
                effects[i] = new SavedSlowEffect(multiplier, remaining);
            }
            return effects;
        });
        var backup = CaptureEffects();
        return new CheckpointRestoreStep(() => RestoreEffects(target), () => RestoreEffects(backup));
    }

    private void RequireCheckpointActor(CheckpointCaptureContext context)
    {
        var identity = CheckpointIdentity;
        if (!isActiveAndEnabled || identity.SlotIndex < 0 || identity.SlotIndex >= context.Config.Players.Count ||
            context.Config.Players[identity.SlotIndex].PlayerId != identity.PlayerId)
            throw new InvalidOperationException("Capture movement effects in their current Player roster.");
        var presence = GetComponent<PlayerMatchPresence>();
        if (presence?.TimeSource != null && (!presence.CanReport || presence.MatchId != context.Config.MatchId))
            throw new InvalidOperationException("Only the authority Player provides movement effects.");
    }

    private SavedSlowEffect[] CaptureEffects()
    {
        double now = GameSeconds;
        var copy = new List<SavedSlowEffect>();
        foreach (var effect in _effects)
            if (effect.EndsAt > now)
                copy.Add(new SavedSlowEffect(effect.Multiplier, effect.EndsAt - now));
        return copy.ToArray();
    }

    private void RestoreEffects(SavedSlowEffect[] saved)
    {
        _effects.Clear();
        double now = GameSeconds;
        foreach (var effect in saved)
            _effects.Add(new SlowEffect(effect.Multiplier, now + effect.RemainingSeconds));
        PublishChanged();
    }
    private readonly struct SavedSlowEffect
    {
        public readonly float Multiplier;
        public readonly double RemainingSeconds;
        public SavedSlowEffect(float multiplier, double remainingSeconds)
        {
            Multiplier = multiplier;
            RemainingSeconds = remainingSeconds;
        }
    }

}

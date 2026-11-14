using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class PlayerExperience : IGrowthCheckpointParticipant
{
    string IGrowthCheckpointParticipant.PersistenceKey => "experience";
    public event Action<GrowthSaveUrgency> GrowthChanged;

    private PlayerActorIdentity CheckpointIdentity => GetComponent<PlayerActorIdentity>() ??
        throw new InvalidOperationException("Bind the Player identity before saving experience.");
    string ICheckpointParticipant.Key => CheckpointIdentity.CheckpointKeyPrefix + "/experience";
    int ICheckpointParticipant.Version => 1;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.Progression;
    int ICheckpointParticipant.RestoreOrder => 230;

    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context);
        return codec.Write(((ICheckpointParticipant)this).Key, 1, CheckpointSectionKind.Progression, writer =>
        {
            codec.WriteText(writer, CheckpointIdentity.PlayerId.Value);
            writer.Write(State.FirstLevelExperience); writer.Write(State.ExperienceIncreasePerLevel); writer.Write(State.MaximumLevel);
            writer.Write(TotalExperience);
        });
    }

    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context.Current);
        long target = codec.Read(section, 1, reader =>
        {
            if (codec.ReadText(reader) != CheckpointIdentity.PlayerId.Value ||
                reader.ReadInt32() != State.FirstLevelExperience || reader.ReadInt32() != State.ExperienceIncreasePerLevel ||
                reader.ReadInt32() != State.MaximumLevel) throw new InvalidDataException("Saved Player experience configuration does not match.");
            long total = reader.ReadInt64();
            if (total < 0 || total > State.TotalRequiredForLevel(State.MaximumLevel)) throw new InvalidDataException("Invalid saved Player experience.");
            return total;
        });
        long backup = TotalExperience;
        return new CheckpointRestoreStep(() => RestoreExperience(target), () => RestoreExperience(backup));
    }

    private void RestoreExperience(long total) { State.RestoreTotalExperience(total); Changed?.Invoke(); }
    private void RequireCheckpointPlayer(CheckpointCaptureContext context)
    {
        var identity = CheckpointIdentity;
        if ((_matchBound && !_authority) || context == null || identity.SlotIndex < 0 || identity.SlotIndex >= context.Config.Players.Count ||
            context.Config.Players[identity.SlotIndex].PlayerId != identity.PlayerId ||
            (_matchBound && _matchId != context.Config.MatchId)) throw new InvalidOperationException("Only the current authority Player can save experience.");
    }
}

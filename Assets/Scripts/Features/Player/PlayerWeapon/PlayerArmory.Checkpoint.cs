using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class PlayerArmory : ICheckpointParticipant
{
    private PlayerActorIdentity Identity => GetComponent<PlayerActorIdentity>();
    string ICheckpointParticipant.Key => Identity.CheckpointKeyPrefix + "/armory";
    int ICheckpointParticipant.Version => 1;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.Progression;
    // 선택 무기 writer(220)가 복원되기 전에 장착 가능 목록을 복원합니다.
    int ICheckpointParticipant.RestoreOrder => 210;
    private void RequirePlayer(CheckpointCaptureContext context)
    {
        if (!IsConfigured || IsRemoteClient || Identity == null || context == null || Identity.SlotIndex < 0 ||
            Identity.SlotIndex >= context.Config.Players.Count || context.Config.Players[Identity.SlotIndex].PlayerId != Identity.PlayerId)
            throw new InvalidOperationException("Only the current Player can save its armory.");
        var presence = GetComponent<PlayerMatchPresence>();
        if (presence?.TimeSource != null && (!presence.CanReport || presence.TimeSource.Snapshot.MatchId != context.Config.MatchId))
            throw new InvalidOperationException("Armory belongs to another Match.");
    }
    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequirePlayer(context);
        return codec.Write(((ICheckpointParticipant)this).Key, 1, CheckpointSectionKind.Progression, writer =>
        {
            codec.WriteText(writer, Identity.PlayerId.Value); writer.Write(_settings.TokensPerLevel); _state.Write(writer);
        });
    }
    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequirePlayer(context.Current);
        var target = codec.Read(section, 1, reader =>
        {
            if (codec.ReadText(reader) != Identity.PlayerId.Value || reader.ReadInt32() != _settings.TokensPerLevel)
                throw new InvalidDataException("Armory owner or reward configuration differs.");
            var state = PlayerArmoryState.Read(reader, _settings.TokensPerLevel);
            if (state.RewardedLevel > _experience.MaximumLevel || !_selection.IsWeaponAuthored(state.First) || !_selection.IsWeaponAuthored(state.Second))
                throw new InvalidDataException("Saved loadout requires missing weapons.");
            return state;
        });
        var backup = _state.Clone();
        return new CheckpointRestoreStep(() => ApplyState(target), () => ApplyState(backup));
    }
    private void ApplyState(PlayerArmoryState state)
    { Close(); _state = state.Clone(); NotifyLoadout(); }
}

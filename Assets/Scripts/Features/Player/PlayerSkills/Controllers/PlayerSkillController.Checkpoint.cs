// Player의 Q/E 스킬 선택을 별도 World section으로 보존합니다.
// 현재 UI 잠금/메뉴를 복원하거나 스킬 사용 비용을 다시 실행하지 않습니다.
using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class PlayerSkillController : ICheckpointParticipant
{
    private PlayerActorIdentity CheckpointIdentity => GetComponent<PlayerActorIdentity>() ?? throw new InvalidOperationException("Bind the Player actor before loadout checkpoints.");
    string ICheckpointParticipant.Key => CheckpointIdentity.CheckpointKeyPrefix + "/loadout";
    int ICheckpointParticipant.Version => 1;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
    int ICheckpointParticipant.RestoreOrder => 230;
    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context); PlayerSkillId q = Loadout.Q, e = Loadout.E;
        return codec.Write(((ICheckpointParticipant)this).Key, 1, CheckpointSectionKind.World, writer =>
        { codec.WriteText(writer, CheckpointIdentity.PlayerId.Value); writer.Write((int)q); writer.Write((int)e); });
    }
    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context.Current);
        var target = codec.Read(section, 1, reader =>
        {
            if (codec.ReadText(reader) != CheckpointIdentity.PlayerId.Value) throw new InvalidDataException("The loadout belongs to a different Player.");
            var q = (PlayerSkillId)reader.ReadInt32(); var e = (PlayerSkillId)reader.ReadInt32();
            try { new PlayerSkillLoadout().Restore(q, e); }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid saved skill selection.", error); }
            if ((q != PlayerSkillId.None && !IsAvailable(q)) || (e != PlayerSkillId.None && !IsAvailable(e)))
                throw new InvalidDataException("The saved skill is not configured on this Player.");
            return (q, e);
        });
        PlayerSkillId beforeQ = Loadout.Q, beforeE = Loadout.E;
        return new CheckpointRestoreStep(() => Loadout.Restore(target.q, target.e), () => Loadout.Restore(beforeQ, beforeE));
    }
    private void RequireCheckpointPlayer(CheckpointCaptureContext context)
    {
        var identity = CheckpointIdentity;
        if (context == null || identity.SlotIndex < 0 || identity.SlotIndex >= context.Config.Players.Count || context.Config.Players[identity.SlotIndex].PlayerId != identity.PlayerId)
            throw new InvalidOperationException("Use the loadout writer only in its current Player roster.");
        var presence = GetComponent<PlayerMatchPresence>();
        if (presence?.TimeSource != null && (presence.TimeSource.Snapshot.MatchId != context.Config.MatchId || !presence.CanReport))
            throw new InvalidOperationException("Only the current Host loadout can provide checkpoint state.");
    }
}

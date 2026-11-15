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
    int ICheckpointParticipant.Version => 4;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
    int ICheckpointParticipant.RestoreOrder => 230;
    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context); PlayerSkillId q = Loadout.Q, e = Loadout.E;
        var research = Research != null ? Research.Capture() : null;
        return codec.Write(((ICheckpointParticipant)this).Key, 4, CheckpointSectionKind.World, writer =>
        { codec.WriteText(writer, CheckpointIdentity.PlayerId.Value); writer.Write((int)q); writer.Write((int)e);
            writer.Write(research != null); research?.Write(writer); });
    }
    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context.Current);
        if (section.Version != 3 && section.Version != 4) throw new InvalidDataException("Unsupported skill research version.");
        var target = codec.Read(section, section.Version, reader =>
        {
            if (codec.ReadText(reader) != CheckpointIdentity.PlayerId.Value) throw new InvalidDataException("The loadout belongs to a different Player.");
            var q = (PlayerSkillId)reader.ReadInt32(); var e = (PlayerSkillId)reader.ReadInt32();
            var research = reader.ReadBoolean() ? PlayerSkillResearch.Snapshot.Read(reader, section.Version == 3) : null;
            if ((Research != null) != (research != null)) throw new InvalidDataException("Skill research configuration differs.");
            try { new PlayerSkillLoadout().Restore(q, e); }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid saved skill selection.", error); }
            if ((q != PlayerSkillId.None && !IsConfiguredSkill(q)) || (e != PlayerSkillId.None && !IsConfiguredSkill(e)))
                throw new InvalidDataException("The saved skill is not configured on this Player.");
            if (research != null)
            {
                foreach(var id in research.Candidates) if (id != PlayerSkillId.None && !IsConfiguredSkill(id))
                    throw new InvalidDataException("The saved candidate is not configured.");
                if ((q != PlayerSkillId.None && Array.IndexOf(research.Candidates, q) < 0) ||
                    (e != PlayerSkillId.None && Array.IndexOf(research.Candidates, e) < 0))
                    throw new InvalidDataException("The saved Q/E skill is not a candidate.");
            }
            return (q, e, research);
        });
        PlayerSkillId beforeQ = Loadout.Q, beforeE = Loadout.E;
        var beforeResearch = Research != null ? Research.Capture() : null;
        return new CheckpointRestoreStep(() => { if (target.research != null) Research.Restore(target.research); Loadout.Restore(target.q, target.e); },
            () => { if (beforeResearch != null) Research.Restore(beforeResearch); Loadout.Restore(beforeQ, beforeE); });
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

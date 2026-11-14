// 개별 스킬의 명시 schema를 현재 Player 슬롯과 권위 Match에 연결합니다.
// 각 파생 스킬이 실제 수량·기한·소유 생성물의 캡처 및 검증/복원을 제공합니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public abstract partial class PlayerSkillBase : ICheckpointParticipant
{
    private PlayerActorIdentity CheckpointIdentity => GetComponent<PlayerActorIdentity>() ?? throw new InvalidOperationException("Bind the Player actor before skill checkpoints.");
    string ICheckpointParticipant.Key => CheckpointIdentity.CheckpointKeyPrefix + "/skill/" + (int)Id;
    protected virtual int CheckpointVersion => 1;
    int ICheckpointParticipant.Version => CheckpointVersion;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
    int ICheckpointParticipant.RestoreOrder => 240 + (int)Id;

    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context);
        Action<BinaryWriter> write = CaptureCheckpointState(codec);
        return codec.Write(((ICheckpointParticipant)this).Key, CheckpointVersion, CheckpointSectionKind.World, writer =>
        { codec.WriteText(writer, CheckpointIdentity.PlayerId.Value); write(writer); });
    }
    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context.Current);
        return codec.Read(section, CheckpointVersion, reader =>
        {
            if (codec.ReadText(reader) != CheckpointIdentity.PlayerId.Value) throw new InvalidDataException("The skill belongs to a different logical Player.");
            try { return PrepareCheckpointState(reader, codec); }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid saved skill state or definition.", error); }
        });
    }
    protected abstract Action<BinaryWriter> CaptureCheckpointState(ICheckpointPayloadCodec codec);
    protected abstract ICheckpointRestoreStep PrepareCheckpointState(BinaryReader reader, ICheckpointPayloadCodec codec);

    private void RequireCheckpointPlayer(CheckpointCaptureContext context)
    {
        var identity = CheckpointIdentity;
        if (context == null || identity.SlotIndex < 0 || identity.SlotIndex >= context.Config.Players.Count ||
            context.Config.Players[identity.SlotIndex].PlayerId != identity.PlayerId)
            throw new InvalidOperationException("Use the skill writer only in its Player roster.");
        var presence = GetComponent<PlayerMatchPresence>();
        if (presence?.TimeSource != null && (presence.TimeSource.Snapshot.MatchId != context.Config.MatchId || !presence.CanReport))
            throw new InvalidOperationException("Only the current Host skill can provide checkpoint state.");
    }
}

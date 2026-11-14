using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class PlayerBaseStats : IGrowthCheckpointParticipant
{
    string IGrowthCheckpointParticipant.PersistenceKey => "base-stats";
    public event Action<GrowthSaveUrgency> GrowthChanged;

    private PlayerActorIdentity CheckpointIdentity => GetComponent<PlayerActorIdentity>() ??
        throw new InvalidOperationException("Bind the Player identity before base stats checkpoints.");
    string ICheckpointParticipant.Key => CheckpointIdentity.CheckpointKeyPrefix + "/base-stats";
    int ICheckpointParticipant.Version => 1;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.Progression;
    int ICheckpointParticipant.RestoreOrder => 240;

    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context);
        var saved = State.Capture();
        return codec.Write(((ICheckpointParticipant)this).Key, 1, CheckpointSectionKind.Progression, writer =>
        {
            codec.WriteText(writer, CheckpointIdentity.PlayerId.Value);
            writer.Write(_experience.MaximumLevel); writer.Write(_settings.AttackPerPoint); writer.Write(_settings.MovementPerPoint);
            writer.Write(_settings.CriticalChancePerPoint); writer.Write(_settings.CriticalChanceCap);
            writer.Write(_settings.CriticalDamageMultiplier); writer.Write(_settings.PointsPerLevelReward);
            writer.Write(saved.Attack); writer.Write(saved.Movement); writer.Write(saved.Critical);
            writer.Write(saved.ClaimedLevels.Length);
            foreach (int level in saved.ClaimedLevels) writer.Write(level);
        });
    }
    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context,
        CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointPlayer(context.Current);
        var target = codec.Read(section, 1, reader =>
        {
            if (codec.ReadText(reader) != CheckpointIdentity.PlayerId.Value || reader.ReadInt32() != _experience.MaximumLevel ||
                reader.ReadSingle() != _settings.AttackPerPoint || reader.ReadSingle() != _settings.MovementPerPoint ||
                reader.ReadSingle() != _settings.CriticalChancePerPoint || reader.ReadSingle() != _settings.CriticalChanceCap ||
                reader.ReadSingle() != _settings.CriticalDamageMultiplier || reader.ReadInt32() != _settings.PointsPerLevelReward)
                throw new InvalidDataException("Saved Player base stats configuration does not match.");
            int attack = reader.ReadInt32(), movement = reader.ReadInt32(), critical = reader.ReadInt32();
            var levels = new int[codec.ReadCount(reader, _experience.MaximumLevel - 1)];
            for (int i = 0; i < levels.Length; i++) levels[i] = reader.ReadInt32();
            var saved = new PlayerBaseStatsState.Snapshot(attack, movement, critical, levels);
            var validator = new PlayerBaseStatsState(_experience.MaximumLevel, _settings.CriticalPointLimit, _settings.PointsPerLevelReward);
            validator.Restore(saved); // 원본 변경 전에 중복 수령·초과 투자·잘못된 레벨을 거부합니다.
            return saved;
        });
        var backup = State.Capture();
        return new CheckpointRestoreStep(() => RestoreStats(target), () => RestoreStats(backup));
    }
    private void RestoreStats(PlayerBaseStatsState.Snapshot saved) { State.Restore(saved); Changed?.Invoke(); }
    private void RequireCheckpointPlayer(CheckpointCaptureContext context)
    {
        var identity = CheckpointIdentity;
        if (!IsConfigured || (_matchBound && !_authority) || context == null || identity.SlotIndex < 0 ||
            identity.SlotIndex >= context.Config.Players.Count || context.Config.Players[identity.SlotIndex].PlayerId != identity.PlayerId ||
            (_matchBound && _matchId != context.Config.MatchId))
            throw new InvalidOperationException("Only the current authority Player can save base stats.");
    }
}

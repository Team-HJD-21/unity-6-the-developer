// 현재 Encounter의 첫 요청·예약 요청·실제 분산 이력을 저장합니다.
// 진단 기록과 이미 처리한 요청 장부는 실행 세대의 관측/중복 방지 자료로 유지합니다.
// 동적 source의 개체 복원은 해당 Feature writer가 제공되어야 합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

/// <summary>
/// 초기 Encounter와 지연 요청, 분산 이력의 저장·복원 연결을 제공한다.
/// </summary>
public sealed partial class EncounterSceneBinding : ICheckpointParticipant, ICheckpointParticipantProvider
{
    string ICheckpointParticipant.Key => "world/encounter";
    int ICheckpointParticipant.Version => 2;
    CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
    int ICheckpointParticipant.RestoreOrder => 300;

    IEnumerable<ICheckpointParticipant> ICheckpointParticipantProvider.GetCheckpointParticipants()
    {
        yield return this;
        if (ExecutionTarget != null) yield return ExecutionTarget.CheckpointWriter;
    }

    CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointMatch(context.Config.MatchId);
        var saved = CaptureEncounter();
        return codec.Write("world/encounter", 2, CheckpointSectionKind.World, w => WriteEncounter(w, saved, codec));
    }

    ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        RequireCheckpointMatch(context.Current.Config.MatchId);
        if (section.Version is not (1 or 2)) throw new InvalidDataException("Unsupported Encounter checkpoint version.");
        var target = codec.Read(section, section.Version, r => ReadEncounter(r, codec, section.Version));
        var backup = CaptureEncounter();
        return new CheckpointRestoreStep(() => ApplyEncounter(target), () => ApplyEncounter(backup));
    }

    private void RequireCheckpointMatch(MatchId match)
    {
        if (_boundMatchId != match || !IsServer || _spawnClock == null || _pipeline == null)
            throw new InvalidOperationException("Bind the authority Encounter before checkpoint operations.");
    }

    /// <summary>
    /// 초기 요청과 대기 중인 예약의 남은 시간, 생성 분산 이력을 저장한다.
    /// </summary>
    private SavedEncounter CaptureEncounter()
    {
        var requests = _pendingSpawns.Values.Where(p => p.Reservation.IsPending).Select(p =>
        {
            if (p.Request.Origin.Kind == SpawnOriginKind.DynamicSource)
                throw new NotSupportedException("A pending dynamic source requires its Feature checkpoint writer and generation rebinding.");
            return new SavedPending(p.Request.PresetId.Value, p.Request.MaximumEnemyCount, p.Request.Source, p.Request.Origin,
                p.Request.CauseEventId, Math.Max(0, p.Reservation.DueGameSeconds - GameSeconds), p.Request.UsesAutomaticComposition);
        }).ToArray();
        return new SavedEncounter(_initialEncounterPending, Math.Max(0, _initialEncounterDue - GameSeconds), requests,
            _pipeline.CaptureCheckpoint(), _pipeline.CapturedBudget);
    }

    /// <summary>
    /// 저장된 요청 대기 상태와 분산 이력을 현재 Match에 다시 연결한다.
    /// </summary>
    private void ApplyEncounter(SavedEncounter saved)
    {
        CancelPendingInitialEncounter(); CancelScheduledEncounters();
        _pipeline.RestoreCheckpoint(saved.History, _boundMatchId);
        _pipeline.RestoreBudget(saved.Budget);
        _initialEncounterPending = saved.InitialPending; _initialEncounterDue = GameSeconds + saved.InitialRemaining;
        if (_initialEncounterPending) StartInitialEncounterIfBound();
        foreach (var pending in saved.Pending)
        {
            // 복원 뒤에는 새 실행 의도입니다. 과거에 이미 소비된 요청 ID를 재사용하지 않습니다.
            var request = pending.Automatic
                ? EncounterRequest.Automatic(new MatchRequestContext(_boundMatchId, Guid.NewGuid(), _authorityIssuer),
                    pending.Maximum, pending.Source, pending.Origin, pending.Cause)
                : new EncounterRequest(new MatchRequestContext(_boundMatchId, Guid.NewGuid(), _authorityIssuer),
                    new DefinitionId(pending.Preset), pending.Maximum, pending.Source, pending.Origin, pending.Cause);
            if (!TryScheduleEncounter(request, pending.Remaining, out _))
                throw new InvalidOperationException("Could not restore a scheduled Encounter request.");
        }
    }

    /// <summary>
    /// Encounter의 초기 요청·예약·분산 이력을 저장 payload에 기록한다.
    /// </summary>
    private static void WriteEncounter(BinaryWriter w, SavedEncounter saved, ICheckpointPayloadCodec codec)
    {
        w.Write(saved.InitialPending); w.Write(saved.InitialRemaining); saved.History.Write(w, codec); w.Write(saved.Budget);
        w.Write(saved.Pending.Length);
        foreach (var p in saved.Pending)
        {
            w.Write(p.Automatic); codec.WriteText(w, p.Preset); w.Write(p.Maximum); w.Write((int)p.Source); w.Write(p.Cause.ToByteArray()); w.Write(p.Remaining);
            w.Write((int)p.Origin.Kind); w.Write((int)p.Origin.Delivery);
            if (p.Origin.Kind == SpawnOriginKind.AuthoredPoint) codec.WriteText(w, p.Origin.PointId.Value);
            else if (p.Origin.Area != null)
            {
                var area = p.Origin.Area;
                w.Write(area.Center.X); w.Write(area.Center.Y); w.Write(area.Width); w.Write(area.Height); w.Write(area.VisualZ);
            }
        }
    }

    /// <summary>
    /// 저장 payload에서 Encounter의 요청 대기 상태와 분산 이력을 읽는다.
    /// </summary>
    private static SavedEncounter ReadEncounter(BinaryReader r, ICheckpointPayloadCodec codec, int version)
    {
        bool initial = r.ReadBoolean(); double remaining = r.ReadDouble();
        if (!double.IsFinite(remaining) || remaining < 0) throw new InvalidDataException("Invalid initial Encounter delay.");
        var history = SpawnDistributionHistory.State.Read(r, codec);
        int budget = version >= 2 ? r.ReadInt32() : 0;
        if (budget < 0) throw new InvalidDataException("Invalid Encounter budget state.");
        var pending = new SavedPending[codec.ReadCount(r, 64)];
        for (int i = 0; i < pending.Length; i++)
        {
            bool automatic = version >= 2 && r.ReadBoolean();
            string preset = codec.ReadText(r); int maximum = r.ReadInt32(); var source = (EncounterRequestSource)r.ReadInt32();
            byte[] cause = r.ReadBytes(16); if (cause.Length != 16) throw new EndOfStreamException();
            double delay = r.ReadDouble(); var kind = (SpawnOriginKind)r.ReadInt32(); var delivery = (SpawnDeliveryMode)r.ReadInt32();
            EncounterSpawnOrigin origin = kind switch
            {
                SpawnOriginKind.Automatic when delivery == SpawnDeliveryMode.Immediate => EncounterSpawnOrigin.Automatic,
                SpawnOriginKind.AuthoredPoint when delivery == SpawnDeliveryMode.Immediate => EncounterSpawnOrigin.AtPoint(new DefinitionId(codec.ReadText(r))),
                SpawnOriginKind.WorldPoint or SpawnOriginKind.WorldArea => EncounterSpawnOrigin.AtWorld(new SpawnArea(new BattlefieldPoint(r.ReadDouble(), r.ReadDouble()),
                    r.ReadDouble(), r.ReadDouble(), r.ReadDouble()), delivery),
                _ => throw new InvalidDataException("Unsupported saved Encounter origin.")
            };
            if (origin.Kind != kind || (!automatic && string.IsNullOrWhiteSpace(preset)) || preset.Length > 128 || maximum < 1 ||
                !Enum.IsDefined(typeof(EncounterRequestSource), source) || !double.IsFinite(delay) || delay < 0)
                throw new InvalidDataException("Invalid scheduled Encounter request.");
            pending[i] = new SavedPending(preset, maximum, source, origin, new Guid(cause), delay, automatic);
        }
        return new SavedEncounter(initial, remaining, pending, history, budget);
    }

    private sealed class SavedEncounter
    {
        public readonly bool InitialPending; public readonly double InitialRemaining; public readonly int Budget;
        public readonly SavedPending[] Pending; public readonly SpawnDistributionHistory.State History;
        public SavedEncounter(bool initial, double remaining, SavedPending[] pending, SpawnDistributionHistory.State history, int budget)
        { InitialPending = initial; InitialRemaining = remaining; Pending = pending; History = history; Budget = budget; }
    }
    private sealed class SavedPending
    {
        public readonly bool Automatic; public readonly string Preset; public readonly int Maximum; public readonly EncounterRequestSource Source;
        public readonly EncounterSpawnOrigin Origin; public readonly Guid Cause; public readonly double Remaining;
        public SavedPending(string preset, int maximum, EncounterRequestSource source, EncounterSpawnOrigin origin, Guid cause, double remaining, bool automatic)
        { Preset = preset; Maximum = maximum; Source = source; Origin = origin; Cause = cause; Remaining = remaining; Automatic = automatic; }
    }
}

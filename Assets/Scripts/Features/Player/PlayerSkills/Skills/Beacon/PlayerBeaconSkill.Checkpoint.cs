// 비콘의 재사용 시간과 실제 활성 장비의 HP·남은 수명·논리 ID를 보존합니다.
using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class PlayerBeaconSkill
{
    protected override Action<BinaryWriter> CaptureCheckpointState(ICheckpointPayloadCodec codec)
    {
        Saved state = CaptureBeacon();
        return writer => { writer.Write(state.Cooldown); writer.Write(state.Placing); writer.Write(state.Device != null); state.Device?.Write(writer, codec); };
    }
    protected override ICheckpointRestoreStep PrepareCheckpointState(BinaryReader reader, ICheckpointPayloadCodec codec)
    {
        float cooldown = PlayerSkillCheckpointIO.ReadDuration(reader);
        bool placing = reader.ReadBoolean();
        var device = reader.ReadBoolean() ? PlayerFieldDevice.CheckpointState.Read(reader, codec) : null;
        if (placing && !HasPlacementPreview) throw new InvalidDataException("The saved beacon preview is not authored.");
        if (device != null && (_prefab == null || device.Kind != PlayerFieldDeviceKind.Beacon)) throw new InvalidDataException("The saved beacon definition differs from this Player.");
        if (device != null) CheckpointActorFactory.ValidatePrefab(_prefab.gameObject, device.Id);
        var target = new Saved(cooldown, device, placing); Saved backup = CaptureBeacon();
        return new CheckpointRestoreStep(() => Apply(target), () => Apply(backup));
    }
    private Saved CaptureBeacon() => new(_remainingCooldown, _active != null && _active.IsAlive ? _active.CaptureCheckpoint() : null, IsPlacing);
    private void Apply(Saved state)
    {
        RestorePlacementState(state.Placing);
        if (_active != null) CheckpointActorFactory.Retire(_active.gameObject);
        _active = null; _remainingCooldown = state.Cooldown;
        if (state.Device == null) return;
        var actor = CheckpointActorFactory.Instantiate(_prefab.gameObject, gameObject.scene, state.Device.Pose, state.Device.Id);
        try { _active = actor.GetComponent<PlayerFieldDevice>(); _active.RestoreCheckpoint(state.Device, transform); FieldSimulationTime.BindSpawned(actor, this); }
        catch { _active = null; CheckpointActorFactory.Retire(actor); throw; }
    }
    private sealed class Saved
    {
        public readonly float Cooldown; public readonly bool Placing; public readonly PlayerFieldDevice.CheckpointState Device;
        public Saved(float cooldown, PlayerFieldDevice.CheckpointState device, bool placing) { Cooldown = cooldown; Device = device; Placing = placing; }
    }
}

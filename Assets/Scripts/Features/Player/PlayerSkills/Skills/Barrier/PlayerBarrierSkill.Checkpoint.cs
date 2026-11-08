// 최신 클릭 설치의 충전·첫 본체 선택과 실제 소유 방벽을 저장합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public sealed partial class PlayerBarrierSkill
{
    protected override Action<BinaryWriter> CaptureCheckpointState(ICheckpointPayloadCodec codec)
    {
        Saved state = CaptureBarriers();
        return writer =>
        {
            PlayerSkillCheckpointIO.Write(writer, state.Charges); writer.Write(state.Pending); writer.Write(state.HasFirst);
            PlayerSkillCheckpointIO.Write(writer, state.First); writer.Write(state.Devices.Length);
            foreach (var device in state.Devices) device.Write(writer, codec);
        };
    }
    protected override ICheckpointRestoreStep PrepareCheckpointState(BinaryReader reader, ICheckpointPayloadCodec codec)
    {
        var charges = PlayerSkillCheckpointIO.ReadCharges(reader); bool pending = reader.ReadBoolean();
        bool firstSelected = reader.ReadBoolean(); Vector3 first = PlayerSkillCheckpointIO.ReadVector(reader);
        var devices = new PlayerFieldDevice.CheckpointState[codec.ReadCount(reader, PlayerSkillCheckpointIO.MaximumOwnedActors)];
        var ids = new HashSet<TeamHJD.Game.Domain.EntityId>();
        for (int i = 0; i < devices.Length; i++)
        {
            devices[i] = PlayerFieldDevice.CheckpointState.Read(reader, codec);
            if (devices[i].Kind != PlayerFieldDeviceKind.Barrier || !ids.Add(devices[i].Id)) throw new InvalidDataException("Invalid or duplicate saved barrier.");
        }
        if (charges.Maximum != _maxCharges || charges.RechargeSeconds != RechargeSeconds || devices.Length > _maxCharges ||
            (firstSelected && !pending) || (pending && !HasPlacementPreview) || (devices.Length > 0 && _prefab == null)) throw new InvalidDataException("The saved barrier definition differs from this Player.");
        foreach (var device in devices) CheckpointActorFactory.ValidatePrefab(_prefab.gameObject, device.Id);
        var target = new Saved(charges, pending, firstSelected, first, devices); Saved backup = CaptureBarriers();
        return new CheckpointRestoreStep(() => Apply(target), () => Apply(backup));
    }
    private Saved CaptureBarriers()
    {
        var devices = _deployed.Where(d => d != null && d.IsAlive).Select(d => d.CaptureCheckpoint()).ToArray();
        if (devices.Length > PlayerSkillCheckpointIO.MaximumOwnedActors) throw new InvalidOperationException("The barrier snapshot exceeds its limit.");
        return new Saved(_charges.CaptureState(), IsPending, _hasFirst, _first, devices);
    }
    private void Apply(Saved state)
    {
        CancelPlacement();
        foreach (var device in _deployed) if (device != null) CheckpointActorFactory.Retire(device.gameObject);
        _deployed.Clear(); _charges.RestoreState(state.Charges);
        foreach (var saved in state.Devices)
        {
            var actor = CheckpointActorFactory.Instantiate(_prefab.gameObject, gameObject.scene, saved.Pose, saved.Id);
            try
            {
                var device = actor.GetComponent<PlayerFieldDevice>(); device.RestoreCheckpoint(saved, transform);
                FieldSimulationTime.BindSpawned(actor, this); _deployed.Add(device);
            }
            catch { CheckpointActorFactory.Retire(actor); throw; }
        }
        RestorePlacementState(state.Pending);
        _first = state.First;
        _hasFirst = state.HasFirst;
    }
    private sealed class Saved
    {
        public readonly PlayerSkillCharges.State Charges; public readonly bool Pending, HasFirst;
        public readonly Vector3 First; public readonly PlayerFieldDevice.CheckpointState[] Devices;
        public Saved(PlayerSkillCharges.State charges, bool pending, bool firstSelected, Vector3 first, PlayerFieldDevice.CheckpointState[] devices)
        { Charges = charges; Pending = pending; HasFirst = firstSelected; First = first; Devices = devices; }
    }
}

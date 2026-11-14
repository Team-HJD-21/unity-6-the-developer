using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public sealed partial class PlayerPortalSkill
{
    // 포털은 외부 Entity 참조를 노출하지 않습니다. 슬롯별 존재·좌표는 이 스킬 section만 소유합니다.
    protected override Action<BinaryWriter> CaptureCheckpointState(ICheckpointPayloadCodec codec)
    {
        Saved state = CapturePortals();
        return writer =>
        {
            writer.Write(state.Placing); writer.Write(state.TravelRemaining);
            for (int slot = 0; slot < 2; slot++)
            {
                writer.Write(state.Positions[slot].HasValue);
                if (state.Positions[slot].HasValue) PlayerSkillCheckpointIO.Write(writer, state.Positions[slot].Value);
            }
        };
    }

    protected override ICheckpointRestoreStep PrepareCheckpointState(BinaryReader reader, ICheckpointPayloadCodec codec)
    {
        bool placing = reader.ReadBoolean(); float remaining = PlayerSkillCheckpointIO.ReadDuration(reader);
        var positions = new Vector3?[2];
        for (int slot = 0; slot < 2; slot++)
            if (reader.ReadBoolean()) positions[slot] = PlayerSkillCheckpointIO.ReadVector(reader);
        if (remaining > Mathf.Max(.1f, _travelCooldown) || (placing || positions[0].HasValue || positions[1].HasValue) && !HasPrefab
            || placing && positions[0].HasValue && positions[1].HasValue)
            throw new InvalidDataException("The saved portal definition or placement state is invalid.");
        if (positions[0].HasValue && positions[1].HasValue
            && ((Vector2)(positions[0].Value - positions[1].Value)).sqrMagnitude < _minimumSeparation * _minimumSeparation)
            throw new InvalidDataException("The saved portal pair overlaps.");
        var target = new Saved(placing, remaining, positions);
        Saved backup = CapturePortals();
        return new CheckpointRestoreStep(() => ApplyPortals(target), () => ApplyPortals(backup));
    }

    private Saved CapturePortals() => new(IsPlacing, TravelRemaining,
        new Vector3?[] { _endpoints[0] != null ? _endpoints[0].transform.position : null,
            _endpoints[1] != null ? _endpoints[1].transform.position : null });

    private void ApplyPortals(Saved state)
    {
        CancelPlacement();
        for (int slot = 0; slot < 2; slot++)
        {
            if (_endpoints[slot] != null)
            {
                _endpoints[slot].gameObject.SetActive(false);
                PlayerFieldDevice.Dispose(_endpoints[slot].gameObject);
            }
            _endpoints[slot] = null;
        }
        for (int slot = 0; slot < 2; slot++)
            if (state.Positions[slot].HasValue) _endpoints[slot] = SpawnEndpoint(slot, state.Positions[slot].Value);
        _travelReadyAt = FieldSimulationTime.Seconds(this) + state.TravelRemaining;
        RestorePlacementState(state.Placing);
        InteractionFailure = "";
    }

    private sealed class Saved
    {
        public readonly bool Placing;
        public readonly float TravelRemaining;
        public readonly Vector3?[] Positions;
        public Saved(bool placing, float remaining, Vector3?[] positions)
        { Placing = placing; TravelRemaining = remaining; Positions = positions; }
    }
}

using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public sealed partial class PlayerPortalSkill
{
    protected override int CheckpointVersion => 3;
    protected override bool SupportsCheckpointVersion(int version) => version >= 1 && version <= 3;
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
            writer.Write(state.ShieldHealth); writer.Write(state.ShieldMaximum); writer.Write(state.ShieldRemaining);
            writer.Write(state.ShieldCooldown);
        };
    }

    protected override ICheckpointRestoreStep PrepareCheckpointState(BinaryReader reader, ICheckpointPayloadCodec codec)
        => PrepareCheckpointState(reader, codec, CheckpointVersion);

    protected override ICheckpointRestoreStep PrepareCheckpointState(BinaryReader reader, ICheckpointPayloadCodec codec, int version)
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
        int shield = version >= 2 ? reader.ReadInt32() : 0, maximum = version >= 2 ? reader.ReadInt32() : 0;
        float shieldRemaining = version >= 2 ? reader.ReadSingle() : 0;
        float shieldCooldown = version >= 3 ? reader.ReadSingle() : shield > 0 ? ShieldCooldown : 0;
        if (shield < 0 || maximum < shield) throw new InvalidDataException("Invalid portal arrival shield health.");
        // v2의 최대 체력 비율 보호막은 현재 고정 보호량으로 제한하고 새 쿨타임을 적용합니다.
        if (version == 2 && shield > 0)
        { maximum = Mathf.Min(maximum, Mathf.CeilToInt(ShieldCapacity)); shield = Mathf.Min(shield, maximum); }
        if (shield < 0 || maximum < shield || !float.IsFinite(shieldRemaining) || shieldRemaining < 0 ||
            shieldRemaining > _shieldDuration * PlayerSkillResearch.MultiplierAtLevel(PlayerSkillResearch.MaximumLevel) ||
            !float.IsFinite(shieldCooldown) || shieldCooldown < 0 || shieldCooldown > ShieldCooldown ||
            (shield == 0 ? maximum != 0 || shieldRemaining != 0 : maximum <= 0 || shieldRemaining <= 0))
            throw new InvalidDataException("Invalid portal arrival shield.");
        var target = new Saved(placing, remaining, positions, shield, maximum, shieldRemaining, shieldCooldown);
        Saved backup = CapturePortals();
        return new CheckpointRestoreStep(() => ApplyPortals(target), () => ApplyPortals(backup));
    }

    private Saved CapturePortals() => new(IsPlacing, TravelRemaining,
        new Vector3?[] { _endpoints[0] != null ? _endpoints[0].transform.position : null,
            _endpoints[1] != null ? _endpoints[1].transform.position : null },
        ShieldHealth, ShieldHealth > 0 ? _shieldMaximum : 0, ShieldHealth > 0 ? ShieldRemaining : 0, ShieldCooldownRemaining);

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
        _arrivalShield = state.ShieldHealth; _shieldMaximum = state.ShieldMaximum;
        _shieldUntil = FieldSimulationTime.Seconds(this) + state.ShieldRemaining;
        _shieldReadyAt = FieldSimulationTime.Seconds(this) + state.ShieldCooldown;
        RestorePlacementState(state.Placing);
        InteractionFailure = "";
    }

    private sealed class Saved
    {
        public readonly bool Placing;
        public readonly float TravelRemaining;
        public readonly Vector3?[] Positions;
        public readonly int ShieldHealth, ShieldMaximum;
        public readonly float ShieldRemaining, ShieldCooldown;
        public Saved(bool placing, float remaining, Vector3?[] positions, int shield, int maximum, float shieldRemaining, float shieldCooldown)
        { Placing = placing; TravelRemaining = remaining; Positions = positions; ShieldHealth = shield; ShieldMaximum = maximum; ShieldRemaining = shieldRemaining; ShieldCooldown = shieldCooldown; }
    }
}

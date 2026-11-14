// Player 스킬 writer의 작은 값·수량 모델을 명시 바이너리 schema로 읽고 씁니다.
// 실제 Scene 검색이나 게임 상태 적용은 수행하지 않습니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using UnityEngine;

internal static class PlayerSkillCheckpointIO
{
    public const int MaximumOwnedActors = 4096;
    public static void Write(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
    public static Vector3 ReadVector(BinaryReader reader)
    {
        var value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z)) throw new InvalidDataException("Invalid saved skill position.");
        return value;
    }
    public static void Write(BinaryWriter writer, PlayerSkillCharges.State state)
    { writer.Write(state.Maximum); writer.Write(state.RechargeSeconds); writer.Write(state.Current); writer.Write(state.Remaining); }
    public static PlayerSkillCharges.State ReadCharges(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadSingle(), reader.ReadInt32(), reader.ReadSingle());
    public static void Write(BinaryWriter writer, PlayerTurretInventory.State state)
    {
        writer.Write(state.Capacity); writer.Write(state.MaximumRepairSeconds); writer.Write(state.Slots.Count);
        foreach (var slot in state.Slots) { writer.Write((int)slot.Status); writer.Write(slot.HealthRatio); writer.Write(slot.Remaining); }
    }
    public static PlayerTurretInventory.State ReadInventory(BinaryReader reader, ICheckpointPayloadCodec codec)
    {
        int capacity = reader.ReadInt32(); float duration = reader.ReadSingle();
        var slots = new PlayerTurretInventory.Slot[codec.ReadCount(reader, MaximumOwnedActors)];
        for (int i = 0; i < slots.Length; i++) slots[i] = new PlayerTurretInventory.Slot(
            (PlayerTurretInventory.SlotStatus)reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle());
        return new PlayerTurretInventory.State(capacity, duration, slots);
    }
    public static float ReadDuration(BinaryReader reader)
    {
        float value = reader.ReadSingle();
        if (!Finite(value) || value < 0) throw new InvalidDataException("Invalid saved skill duration.");
        return value;
    }
    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

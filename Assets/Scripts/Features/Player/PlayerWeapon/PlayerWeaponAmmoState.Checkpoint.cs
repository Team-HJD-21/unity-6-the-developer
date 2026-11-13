using System;
using System.IO;

public sealed partial class PlayerWeaponAmmoState
{
    public CheckpointState CaptureCheckpoint() => new CheckpointState((int[])_rounds.Clone(),
        _reloadWeapon, _reloadRemaining, _reloadDuration);

    public void ValidateCheckpoint(CheckpointState state)
    {
        if (state == null) throw new InvalidDataException("Missing magazine state.");
        for (int i = 0; i < _rounds.Length; i++)
            if (state.Rounds[i] < 0 || state.Rounds[i] > _capacities[i])
                throw new InvalidDataException("Saved rounds exceed the configured magazine.");
        if (!IsFinite(state.Remaining) || !IsFinite(state.Duration) || state.Remaining < 0 ||
            state.Duration < 0 || state.Remaining > state.Duration ||
            (state.Remaining == 0 && state.Duration != 0) ||
            !Enum.IsDefined(typeof(WeaponType), state.Weapon) ||
            (state.Remaining > 0 && (state.Weapon == WeaponType.Spanner ||
                state.Rounds[(int)state.Weapon] >= GetCapacity(state.Weapon))))
            throw new InvalidDataException("Invalid saved reload state.");
    }

    public void RestoreCheckpoint(CheckpointState state)
    {
        ValidateCheckpoint(state);
        Array.Copy(state.Rounds, _rounds, _rounds.Length);
        _reloadWeapon = state.Weapon;
        _reloadRemaining = state.Remaining;
        _reloadDuration = state.Duration;
    }

    public sealed class CheckpointState
    {
        internal readonly int[] Rounds;
        internal readonly WeaponType Weapon;
        internal readonly double Remaining, Duration;

        internal CheckpointState(int[] rounds, WeaponType weapon, double remaining, double duration)
        { Rounds = rounds; Weapon = weapon; Remaining = remaining; Duration = duration; }

        public void Write(BinaryWriter writer)
        {
            foreach (int rounds in Rounds) writer.Write(rounds);
            writer.Write((int)Weapon); writer.Write(Remaining); writer.Write(Duration);
        }

        public static CheckpointState Read(BinaryReader reader) => new CheckpointState(
            new[] { reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32() },
            (WeaponType)reader.ReadInt32(), reader.ReadDouble(), reader.ReadDouble());
    }
}

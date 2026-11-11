using System;
using System.IO;

/// <summary>총기별 탄창과 한 번의 재장전 진행을 보관합니다. 시간은 필드 시계에서 전달합니다.</summary>
public sealed class PlayerWeaponAmmoState
{
    private readonly int[] _capacities;
    private readonly int[] _rounds;
    private WeaponType _reloadWeapon;
    private double _reloadRemaining;
    private double _reloadDuration;

    public bool IsReloading => _reloadRemaining > 0;
    public WeaponType ReloadWeapon => _reloadWeapon;
    public float ReloadProgress => IsReloading ? (float)(1 - _reloadRemaining / _reloadDuration) : 0f;

    public PlayerWeaponAmmoState(int defaultGunCapacity = 15, int cryoCapacity = 15, int sniperCapacity = 5)
    {
        if (defaultGunCapacity <= 0 || cryoCapacity <= 0 || sniperCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(defaultGunCapacity));
        _capacities = new[] { defaultGunCapacity, cryoCapacity, 0, sniperCapacity };
        _rounds = (int[])_capacities.Clone();
    }

    public int GetCapacity(WeaponType weapon) => _capacities[Index(weapon)];
    public int GetRounds(WeaponType weapon) => _rounds[Index(weapon)];
    public bool CanFire(WeaponType weapon) => !IsReloading &&
        (weapon == WeaponType.Spanner || GetRounds(weapon) > 0);

    /// <summary>산탄 수와 무관하게 실제 발사 성공 한 번에 한 발만 소비합니다.</summary>
    public bool TryConsumeRound(WeaponType weapon)
    {
        if (!CanFire(weapon)) return false;
        if (weapon != WeaponType.Spanner) _rounds[Index(weapon)]--;
        return true;
    }

    public bool TryStartReload(WeaponType weapon, double duration)
    {
        if (weapon == WeaponType.Spanner || IsReloading || !IsFinite(duration) || duration <= 0 ||
            GetRounds(weapon) >= GetCapacity(weapon)) return false;
        _reloadWeapon = weapon;
        _reloadDuration = _reloadRemaining = duration;
        return true;
    }

    public void AdvanceReload(double deltaTime)
    {
        if (!IsReloading || !IsFinite(deltaTime) || deltaTime <= 0) return;
        _reloadRemaining = Math.Max(0, _reloadRemaining - deltaTime);
        if (IsReloading) return;
        _rounds[Index(_reloadWeapon)] = GetCapacity(_reloadWeapon);
        CancelReload();
    }

    public void CancelReload() { _reloadRemaining = 0; _reloadDuration = 0; }

    private static int Index(WeaponType weapon)
    {
        int index = (int)weapon;
        if (index < 0 || index > (int)WeaponType.Sniper) throw new ArgumentOutOfRangeException(nameof(weapon));
        return index;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

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

using System;
using System.IO;

/// <summary>플레이 세션에서 이어지는 무기 레벨·장착·레벨 보상입니다. 디스크/백엔드 저장은 하지 않습니다.</summary>
public sealed class PlayerArmoryState
{
    private readonly int[] _levels = { 1, 1, 1 };
    public WeaponType First { get; private set; } = WeaponType.DefaultGun;
    public WeaponType Second { get; private set; } = WeaponType.CryoBlaster;
    public int RewardedLevel { get; private set; }
    public int Tokens { get; private set; }
    public static bool IsGun(WeaponType weapon) => weapon == WeaponType.DefaultGun || weapon == WeaponType.CryoBlaster || weapon == WeaponType.Sniper;
    private static int Index(WeaponType weapon) => weapon == WeaponType.Sniper ? 2 : (int)weapon;
    public int Level(WeaponType weapon) => IsGun(weapon) ? _levels[Index(weapon)] : 1;
    public bool IsEquipped(WeaponType weapon) => weapon == WeaponType.Spanner || weapon == First || weapon == Second;
    public bool Equip(int slot, WeaponType weapon)
    {
        if ((slot != 0 && slot != 1) || !IsGun(weapon)) return false;
        if ((slot == 0 ? First : Second) == weapon) return true;
        // 이미 다른 슬롯에 장착한 무기는 두 슬롯을 교환합니다.
        if (slot == 0) { if (Second == weapon) Second = First; First = weapon; }
        else { if (First == weapon) First = Second; Second = weapon; }
        return true;
    }
    public bool GrantLevelRewards(int achievedLevel, int tokensPerLevel)
    {
        if (achievedLevel < 1 || achievedLevel <= RewardedLevel || achievedLevel > 1000 || tokensPerLevel < 1 || tokensPerLevel > 100) return false;
        Tokens = checked(Tokens + (achievedLevel - RewardedLevel) * tokensPerLevel);
        RewardedLevel = achievedLevel;
        return true;
    }
    public bool CanUpgrade(WeaponType weapon) => IsGun(weapon) && Tokens > 0 && Level(weapon) < PlayerArmorySettings.MaximumLevel;
    public bool Upgrade(WeaponType weapon)
    {
        if (!CanUpgrade(weapon)) return false;
        _levels[Index(weapon)]++; Tokens--; return true;
    }
    internal void Write(BinaryWriter writer)
    {
        writer.Write((int)First); writer.Write((int)Second); writer.Write(RewardedLevel); writer.Write(Tokens);
        foreach (int level in _levels) writer.Write(level);
    }
    internal PlayerArmoryState Clone()
    {
        var copy = new PlayerArmoryState { First = First, Second = Second, RewardedLevel = RewardedLevel, Tokens = Tokens };
        Array.Copy(_levels, copy._levels, _levels.Length); return copy;
    }
    internal static PlayerArmoryState Read(BinaryReader reader, int tokensPerLevel, bool legacy = false)
    {
        var state = new PlayerArmoryState { First = (WeaponType)reader.ReadInt32(), Second = (WeaponType)reader.ReadInt32(),
            RewardedLevel = reader.ReadInt32(), Tokens = reader.ReadInt32() };
        int spent = 0;
        for (int i = 0; i < state._levels.Length; i++)
        {
            int level = reader.ReadInt32();
            if (level < 1 || level > PlayerArmorySettings.MaximumLevel) throw new InvalidDataException("Invalid weapon level.");
            state._levels[i] = level; spent += level - 1;
        }
        if (!IsGun(state.First) || !IsGun(state.Second) || state.First == state.Second || state.RewardedLevel < 1 || state.RewardedLevel > 1000 ||
            tokensPerLevel < 1 || tokensPerLevel > 100 || state.Tokens < 0 ||
            (long)state.Tokens + spent != (state.RewardedLevel - (legacy ? 1 : 0)) * tokensPerLevel)
            throw new InvalidDataException("Invalid armory loadout or token accounting.");
        if (legacy) state.Tokens += tokensPerLevel;
        return state;
    }
}

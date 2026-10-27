/// <summary>단축키·휠·HUD가 사용하는 슬롯 순서입니다. WeaponType의 직렬화 번호와 구분합니다.</summary>
public static class PlayerWeaponSlots
{
    public const int Count = 4;

    public static bool TryGetWeapon(int slot, out WeaponType weapon)
    {
        weapon = slot switch
        {
            1 => WeaponType.Spanner,
            2 => WeaponType.DefaultGun,
            3 => WeaponType.CryoBlaster,
            4 => WeaponType.Sniper,
            _ => default
        };
        return slot >= 1 && slot <= Count;
    }

    public static int GetSlot(WeaponType weapon) => weapon switch
    {
        WeaponType.Spanner => 1,
        WeaponType.DefaultGun => 2,
        WeaponType.CryoBlaster => 3,
        WeaponType.Sniper => 4,
        _ => 0
    };
}

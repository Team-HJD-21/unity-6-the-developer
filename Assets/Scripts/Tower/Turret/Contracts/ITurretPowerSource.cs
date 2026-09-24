namespace TeamHJD.Game.Turrets.Contracts
{
    public interface ITurretPowerSource
    {
        int GetCurrentPower();
        void AddUnit(int power);
        void RemoveUnit(int power);
    }
}

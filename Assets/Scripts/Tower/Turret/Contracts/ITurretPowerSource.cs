using System;

namespace TeamHJD.Game.Turrets.Contracts
{
    public interface ITurretPowerSource
    {
        event Action<int, int> PowerChanged;

        int CurrentPower { get; }
        int MaximumPower { get; }

        bool TryConsumePower(int power);
        void ReleasePower(int power);
    }
}

using System;

namespace TeamHJD.Game.Turrets.Contracts
{
    public interface ITurretPowerSource
    {
        event Action<int, int> PowerChanged;

        int CurrentPower { get; }
        int MaximumPower { get; }

        bool TryConsumePower(int power);
        bool TryChangeReservation(int previousPower, int newPower);
        void ReleasePower(int power);
    }
}

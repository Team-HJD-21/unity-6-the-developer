using System;

namespace TeamHJD.Game.FieldTurrets.Contracts
{
    public interface IFieldTurretPowerSource
    {
        event Action<int, int> PowerChanged;

        int CurrentPower { get; }
        int MaximumPower { get; }

        bool TryConsumePower(int power);
        bool TryChangeReservation(int previousPower, int newPower);
        void ReleasePower(int power);
    }
}

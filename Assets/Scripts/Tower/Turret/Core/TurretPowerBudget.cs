using System;
using TeamHJD.Game.Turrets.Contracts;

namespace TeamHJD.Game.Turrets
{
    /// <summary>Unity와 CU에 의존하지 않는 전력 예약 및 지연 반환 모델.</summary>
    public sealed class TurretPowerBudget : ITurretPowerSource
    {
        private const double RecoveryInterval = 0.1;
        private double _recoveryElapsed;
        public event Action<int, int> PowerChanged;
        public int CurrentPower { get; private set; }
        public int MaximumPower { get; private set; }
        public int PendingRecovery { get; private set; }
        public int ReservedPower => MaximumPower - CurrentPower - PendingRecovery;

        public TurretPowerBudget(int maximumPower)
        {
            if (maximumPower < 0) throw new ArgumentOutOfRangeException(nameof(maximumPower));
            CurrentPower = MaximumPower = maximumPower;
        }

        // Capacity changes must not silently discard existing reservations.
        public bool TrySetMaximumPower(int maximumPower)
        {
            if (maximumPower < 0 || maximumPower < ReservedPower) return false;
            int reserved = ReservedPower;
            MaximumPower = maximumPower;
            PendingRecovery = Math.Min(PendingRecovery, maximumPower - reserved);
            CurrentPower = maximumPower - reserved - PendingRecovery;
            NotifyChanged();
            return true;
        }

        public bool TryConsumePower(int power)
        {
            if (power < 0 || power > CurrentPower) return false;
            CurrentPower -= power;
            NotifyChanged();
            return true;
        }

        public bool TryChangeReservation(int previousPower, int newPower)
        {
            if (previousPower < 0 || newPower < 0 || previousPower > ReservedPower) return false;
            int difference = newPower - previousPower;
            if (difference > 0) return TryConsumePower(difference);
            if (difference < 0)
            {
                CurrentPower -= difference;
                NotifyChanged();
            }
            return true;
        }

        public void ReleasePower(int power)
        {
            if (power <= 0) return;
            int accepted = Math.Min(power, ReservedPower);
            if (PendingRecovery == 0) _recoveryElapsed = 0;
            PendingRecovery += accepted;
        }

        /// <summary>소유 컨트롤러가 전달한 경과 시간으로 0.1초마다 전력 1을 반환한다.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || PendingRecovery == 0)
                return;
            _recoveryElapsed += deltaTime;
            int recovered = (int)Math.Min(PendingRecovery, Math.Floor(_recoveryElapsed / RecoveryInterval));
            if (recovered == 0) return;
            PendingRecovery -= recovered;
            CurrentPower += recovered;
            _recoveryElapsed = PendingRecovery == 0 ? 0 : _recoveryElapsed - recovered * RecoveryInterval;
            NotifyChanged();
        }

        private void NotifyChanged() => PowerChanged?.Invoke(CurrentPower, MaximumPower);
    }
}

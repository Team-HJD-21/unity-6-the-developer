using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    public sealed class TurretActivationController
    {
        private readonly TurretRuntimeState _runtimeState;
        private readonly ITurretPowerSource _powerSource;
        private readonly int _powerCost;

        private bool _hasPowerReservation;

        public TurretActivationController(
            TurretRuntimeState runtimeState,
            ITurretPowerSource powerSource,
            int powerCost)
        {
            _runtimeState = runtimeState;
            _powerSource = powerSource;
            _powerCost = powerCost;
        }

        public TurretActivationResult RequestActivation(bool shouldActivate)
        {
            if (_runtimeState.IsActivated == shouldActivate)
            {
                return TurretActivationResult.Unchanged;
            }

            if (shouldActivate)
            {
                return Activate();
            }

            Deactivate();
            return TurretActivationResult.Deactivated;
        }

        public bool ReleaseForShutdown()
        {
            if (!_runtimeState.IsActivated && !_hasPowerReservation)
            {
                return false;
            }

            _runtimeState.SetActivated(false);
            ReleaseReservedPower();
            return true;
        }

        private TurretActivationResult Activate()
        {
            if (_powerCost < 0)
            {
                return TurretActivationResult.InvalidPowerCost;
            }

            if (!IsPowerSourceAvailable())
            {
                return TurretActivationResult.PowerSourceUnavailable;
            }

            if (!_powerSource.TryConsumePower(_powerCost))
            {
                return TurretActivationResult.InsufficientPower;
            }

            _hasPowerReservation = _powerCost > 0;
            _runtimeState.SetActivated(true);
            return TurretActivationResult.Activated;
        }

        private void Deactivate()
        {
            _runtimeState.SetActivated(false);
            ReleaseReservedPower();
        }

        private void ReleaseReservedPower()
        {
            if (!_hasPowerReservation)
            {
                return;
            }

            if (IsPowerSourceAvailable())
            {
                _powerSource.ReleasePower(_powerCost);
            }

            _hasPowerReservation = false;
        }

        private bool IsPowerSourceAvailable()
        {
            return _powerSource != null &&
                   (_powerSource is not Object unityObject || unityObject != null);
        }
    }
}

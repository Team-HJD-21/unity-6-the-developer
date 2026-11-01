using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed class FieldTurretActivationController
    {
        private readonly FieldTurretRuntimeState _runtimeState;
        private readonly IFieldTurretPowerSource _powerSource;
        private int _powerCost;

        private bool _hasPowerReservation;

        public FieldTurretActivationController(
            FieldTurretRuntimeState runtimeState,
            IFieldTurretPowerSource powerSource,
            int powerCost)
        {
            _runtimeState = runtimeState;
            _powerSource = powerSource;
            _powerCost = powerCost;
        }

        public FieldTurretActivationResult RequestActivation(bool shouldActivate)
        {
            if (_runtimeState.IsActivated == shouldActivate)
            {
                return FieldTurretActivationResult.Unchanged;
            }

            if (shouldActivate)
            {
                return Activate();
            }

            Deactivate();
            return FieldTurretActivationResult.Deactivated;
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

        internal PowerCostChangeResult TrySetPowerCost(int powerCost)
        {
            if (powerCost < 0)
            {
                return PowerCostChangeResult.InvalidPowerCost;
            }

            if (_powerCost == powerCost)
            {
                return PowerCostChangeResult.Changed;
            }

            if (!_runtimeState.IsActivated)
            {
                _powerCost = powerCost;
                return PowerCostChangeResult.Changed;
            }

            if (!IsPowerSourceAvailable())
            {
                return PowerCostChangeResult.PowerSourceUnavailable;
            }

            if (!_powerSource.TryChangeReservation(_powerCost, powerCost))
            {
                return PowerCostChangeResult.InsufficientPower;
            }

            _powerCost = powerCost;
            _hasPowerReservation = powerCost > 0;
            return PowerCostChangeResult.Changed;
        }

        internal void DetachReservationForLevelUpgrade()
        {
            _hasPowerReservation = false;
            _runtimeState.SetActivated(false);
        }

        internal void AdoptReservationForLevelUpgrade()
        {
            _hasPowerReservation = _powerCost > 0;
            _runtimeState.SetActivated(true);
        }

        private FieldTurretActivationResult Activate()
        {
            if (_powerCost < 0)
            {
                return FieldTurretActivationResult.InvalidPowerCost;
            }

            if (!IsPowerSourceAvailable())
            {
                return FieldTurretActivationResult.PowerSourceUnavailable;
            }

            if (!_powerSource.TryConsumePower(_powerCost))
            {
                return FieldTurretActivationResult.InsufficientPower;
            }

            _hasPowerReservation = _powerCost > 0;
            _runtimeState.SetActivated(true);
            return FieldTurretActivationResult.Activated;
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

    internal enum PowerCostChangeResult
    {
        Changed,
        InsufficientPower,
        PowerSourceUnavailable,
        InvalidPowerCost
    }
}

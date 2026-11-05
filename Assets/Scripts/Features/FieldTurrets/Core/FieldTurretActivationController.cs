using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed class FieldTurretActivationController
    {
        private readonly FieldTurretRuntimeState _runtimeState;
        private readonly IFieldTurretPowerSource _powerSource;
        private int _powerCost;

        private int _reservedPower;
        private bool _destructionSettled;
        public int ReservedPower => _reservedPower;

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
            if (_runtimeState.IsDestroyed || _destructionSettled)
                return FieldTurretActivationResult.Destroyed;
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
            if (_runtimeState.IsDestroyed) return false; // 비활성화만으로 파괴 터렛에 묶인 전력을 회수하지 않는다.
            if (!_runtimeState.IsActivated && _reservedPower == 0)
            {
                return false;
            }

            _runtimeState.SetActivated(false);
            ReleaseReservedPower();
            return true;
        }

        internal void HandleDestruction(float returnPercent)
        {
            if (float.IsNaN(returnPercent) || float.IsInfinity(returnPercent) || returnPercent < 0f || returnPercent > 100f)
                throw new System.ArgumentOutOfRangeException(nameof(returnPercent));
            if (_destructionSettled) return;
            _destructionSettled = true;
            _runtimeState.SetActivated(false);
            int returnedPower = (int)System.Math.Floor(_reservedPower * (double)returnPercent / 100d);
            ReleaseReservedPower(returnedPower); // 반환량은 내림하고 나머지는 이 터렛의 예약으로 유지한다.
        }

        internal void ReleaseAfterRestore()
        {
            ReleaseReservedPower();
            _destructionSettled = false;
        }

        internal void ReleaseForRemoval()
        {
            _runtimeState.SetActivated(false);
            ReleaseReservedPower(); // 실제 객체 제거는 복구할 대상이 없으므로 소유한 예약을 정리한다.
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
            _reservedPower = powerCost;
            return PowerCostChangeResult.Changed;
        }

        internal void DetachReservationForLevelUpgrade()
        {
            _reservedPower = 0;
            _runtimeState.SetActivated(false);
        }

        internal void AdoptReservationForLevelUpgrade()
        {
            _reservedPower = _powerCost;
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

            _reservedPower = _powerCost;
            _runtimeState.SetActivated(true);
            return FieldTurretActivationResult.Activated;
        }

        private void Deactivate()
        {
            _runtimeState.SetActivated(false);
            ReleaseReservedPower();
        }

        private void ReleaseReservedPower() => ReleaseReservedPower(_reservedPower);

        private void ReleaseReservedPower(int power)
        {
            if (power <= 0)
            {
                return;
            }

            _reservedPower -= power;
            if (IsPowerSourceAvailable())
            {
                _powerSource.ReleasePower(power);
            }
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

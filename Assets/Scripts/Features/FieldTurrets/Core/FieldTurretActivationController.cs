// 터렛 활성 상태와 공용 예산의 전력 예약을 연결합니다.
// 체크포인트 적용은 예산 전체 복원과 함께 예약 소유권만 다시 연결하며 신규 소비를 만들지 않습니다.
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
        internal int ReservedPower => _hasPowerReservation ? _powerCost : 0;

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

        // 전체 Feature 복원은 기존 예약을 반환하지 않고 목표 예산으로 교체합니다.
        internal void DetachReservationForCheckpoint() => _hasPowerReservation = false;

        internal void RestoreReservation(int powerCost)
        {
            if (powerCost < 0) throw new System.ArgumentOutOfRangeException(nameof(powerCost));
            if (_hasPowerReservation)
                throw new System.InvalidOperationException("Detach the previous checkpoint reservation before restoring.");
            _powerCost = powerCost;
            _hasPowerReservation = _runtimeState.IsActivated && powerCost > 0;
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

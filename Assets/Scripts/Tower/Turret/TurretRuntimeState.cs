using System;
using UnityEngine;

namespace TeamHjd.Game.Turrets
{
    [Serializable]
    public sealed class TurretRuntimeState
    {
        [SerializeField, Min(0)] private int _instanceId;
        [SerializeField] private bool _isActivated;
        [SerializeField] private int _damageBonus;

        private bool _previousIsActivated;

        public int InstanceId => _instanceId;
        public bool HasInstanceId => _instanceId > 0;
        public bool IsActivated => _isActivated;
        public int DamageBonus => _damageBonus;
        public bool ActivationChanged => _isActivated != _previousIsActivated;

        public void AssignInstanceId(int instanceId)
        {
            if (instanceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instanceId));
            }

            _instanceId = instanceId;
        }

        public void SetDamageBonus(int damageBonus)
        {
            _damageBonus = damageBonus;
        }

        public void AddDamageBonus(int damageBonus)
        {
            _damageBonus += damageBonus;
        }

        public void SetActivated(bool isActivated)
        {
            _isActivated = isActivated;
        }

        public void CommitActivationState()
        {
            _previousIsActivated = _isActivated;
        }

        public void SynchronizeActivationState(bool isActivated)
        {
            _isActivated = isActivated;
            _previousIsActivated = isActivated;
        }
    }
}

using System;
using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    [Serializable]
    public sealed class TurretRuntimeState
    {
        [SerializeField, Min(0)] private int _instanceId;
        [SerializeField] private bool _isActivated;
        [SerializeField] private int _damageBonus;

        [NonSerialized] private int _currentHealth;
        [NonSerialized] private bool _isDestroyed;
        [NonSerialized] private bool _healthInitialized;
        private bool _isTemporarilySuspended;

        public int InstanceId => _instanceId;
        public bool HasInstanceId => _instanceId > 0;
        public bool IsActivated => _isActivated;
        public bool IsOperational => _isActivated && !_isTemporarilySuspended && !_isDestroyed;
        public bool IsTemporarilySuspended => _isTemporarilySuspended;
        public int CurrentHealth => _currentHealth;
        public bool IsDestroyed => _isDestroyed;
        public int DamageBonus => _damageBonus;

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

        internal void SetActivated(bool isActivated)
        {
            _isActivated = isActivated;
        }

        internal void SetTemporarilySuspended(bool isTemporarilySuspended)
        {
            _isTemporarilySuspended = isTemporarilySuspended;
        }

        internal void InitializeHealth(int maxHealth)
        {
            if (_healthInitialized)
            {
                return;
            }

            _currentHealth = Mathf.Max(1, maxHealth);
            _isDestroyed = false;
            _healthInitialized = true;
        }

        internal bool ApplyDamage(int damage)
        {
            if (!_healthInitialized || _isDestroyed || damage <= 0)
            {
                return false;
            }

            int previousHealth = _currentHealth;
            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            return previousHealth != _currentHealth;
        }

        internal void MarkDestroyed()
        {
            _currentHealth = 0;
            _isDestroyed = true;
            _isTemporarilySuspended = false;
        }

        internal bool Restore(int maxHealth)
        {
            if (!_healthInitialized || !_isDestroyed)
            {
                return false;
            }

            _currentHealth = Mathf.Max(1, maxHealth);
            _isDestroyed = false;
            _isActivated = false;
            _isTemporarilySuspended = false;
            return true;
        }
    }
}

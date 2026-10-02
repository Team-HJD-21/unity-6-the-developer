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
        [SerializeField] private bool _isLocked;

        public const int MaximumUpgradeLevel = 5;
        [NonSerialized] private string _selectedUpgradeId;
        [NonSerialized] private int _upgradeLevel;
        [NonSerialized] private float _upgradeDamageModifierRatio;
        [NonSerialized] private float _upgradePowerModifierRatio;
        [NonSerialized] private float _upgradeRangeModifierRatio;
        [NonSerialized] private int _currentHealth;
        [NonSerialized] private bool _isDestroyed;
        [NonSerialized] private bool _healthInitialized;
        private bool _isTemporarilySuspended;

        public int InstanceId => _instanceId;
        public bool HasInstanceId => _instanceId > 0;
        public bool IsActivated => _isActivated;
        public bool IsOperational => _isActivated && !_isTemporarilySuspended && !_isDestroyed && !_isLocked;
        public bool IsLocked => _isLocked;
        public bool IsTemporarilySuspended => _isTemporarilySuspended;
        public int CurrentHealth => _currentHealth;
        public bool IsDestroyed => _isDestroyed;
        public int DamageBonus => _damageBonus;
        public float DamageModifierRatio => _upgradeDamageModifierRatio;
        public float PowerModifierRatio => _upgradePowerModifierRatio;
        public float RangeModifierRatio => _upgradeRangeModifierRatio;
        public string SelectedUpgradeId => _selectedUpgradeId ?? string.Empty;
        public int UpgradeLevel => _upgradeLevel;

        public int GetEffectiveDamage(int definitionDamage) =>
            Mathf.Max(0, CalculateScaledStat(definitionDamage, DamageModifierRatio) + DamageBonus);

        public int GetEffectivePower(int definitionPower) =>
            CalculateScaledStat(definitionPower, PowerModifierRatio);

        internal static int CalculateScaledStat(int baseValue, float modifierRatio) =>
            Mathf.Max(0, Mathf.CeilToInt(baseValue * (1f + modifierRatio)));

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

        public bool HasAppliedUpgrade(string upgradeId)
        {
            return !string.IsNullOrWhiteSpace(upgradeId) &&
                   _upgradeLevel > 0 && _selectedUpgradeId == upgradeId;
        }

        internal void SetUpgradeLevel(
            string upgradeId, int level, float damageModifierRatio, float powerModifierRatio, float rangeModifierRatio)
        {
            _upgradeLevel = Mathf.Clamp(level, 0, MaximumUpgradeLevel);
            _selectedUpgradeId = _upgradeLevel == 0 ? null : upgradeId;
            // Recalculate from the rank rather than accumulating floating point deltas.
            _upgradeDamageModifierRatio = damageModifierRatio * _upgradeLevel;
            _upgradePowerModifierRatio = powerModifierRatio * _upgradeLevel;
            _upgradeRangeModifierRatio = rangeModifierRatio * _upgradeLevel;
        }

        internal void CopyForLevelChange(TurretRuntimeState target, int sourceMaxHealth, int targetMaxHealth)
        {
            target._instanceId = _instanceId;
            target._isActivated = false;
            target._damageBonus = _damageBonus;
            target._upgradeDamageModifierRatio = _upgradeDamageModifierRatio;
            target._upgradePowerModifierRatio = _upgradePowerModifierRatio;
            target._upgradeRangeModifierRatio = _upgradeRangeModifierRatio;
            target._isLocked = _isLocked;
            target._selectedUpgradeId = _selectedUpgradeId;
            target._upgradeLevel = _upgradeLevel;
            target._currentHealth = Mathf.Clamp(
                Mathf.CeilToInt(_currentHealth / (float)Mathf.Max(1, sourceMaxHealth) *
                                Mathf.Max(1, targetMaxHealth)),
                1,
                Mathf.Max(1, targetMaxHealth));
            target._healthInitialized = true;
            target._isDestroyed = false;
            target._isTemporarilySuspended = false;
        }

        internal void SetActivated(bool isActivated)
        {
            _isActivated = isActivated;
        }

        internal void SetLocked(bool isLocked)
        {
            _isLocked = isLocked;
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

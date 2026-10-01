using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    /// <summary>
    /// Values copied from one registered turret at the time of a query.
    /// This type never exposes a turret component or its mutable runtime state.
    /// </summary>
    public readonly struct TurretSnapshot
    {
        public int InstanceId { get; }
        public string DefinitionId { get; }
        public Vector3 Position { get; }
        public bool IsActivated { get; }
        public bool IsOperational { get; }
        public bool IsDestroyed { get; }
        public bool IsLocked { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }
        public int EffectiveDamage { get; }
        public float Range { get; }
        public int EffectivePower { get; }
        public string SelectedUpgradeId { get; }
        public int UpgradeLevel { get; }

        internal TurretSnapshot(
            int instanceId,
            string definitionId,
            Vector3 position,
            bool isActivated,
            bool isOperational,
            bool isDestroyed,
            bool isLocked,
            int currentHealth,
            int maxHealth,
            int effectiveDamage,
            float range,
            int effectivePower,
            string selectedUpgradeId,
            int upgradeLevel)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Position = position;
            IsActivated = isActivated;
            IsOperational = isOperational;
            IsDestroyed = isDestroyed;
            IsLocked = isLocked;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            EffectiveDamage = effectiveDamage;
            Range = range;
            EffectivePower = effectivePower;
            SelectedUpgradeId = selectedUpgradeId;
            UpgradeLevel = upgradeLevel;
        }
    }
}

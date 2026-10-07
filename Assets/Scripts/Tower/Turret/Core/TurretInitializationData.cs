using System;
using TeamHJD.Game.Content;

namespace TeamHJD.Game.Turrets
{
    /// <summary>
    /// Resolved creation input, not a query snapshot or a serialized save format.
    /// Definition/upgrade IDs must be resolved by the caller before creating this input.
    /// Heat, projectiles, reservations and runtime InstanceId are not restored here.
    /// </summary>
    public sealed class TurretInitializationData
    {
        public TurretDefinition Definition { get; }
        public int CurrentHealth { get; }
        public bool IsDestroyed => CurrentHealth == 0;
        public bool IsLocked { get; }
        public bool ShouldActivate { get; }
        public int DamageBonus { get; }
        public TurretUpgradeDefinition Upgrade { get; }
        public int UpgradeLevel { get; }

        public TurretInitializationData(
            TurretDefinition definition, int currentHealth, bool isLocked = false,
            bool shouldActivate = false, int damageBonus = 0,
            TurretUpgradeDefinition upgrade = null, int upgradeLevel = 0)
        {
            Definition = definition;
            CurrentHealth = currentHealth;
            IsLocked = isLocked;
            ShouldActivate = shouldActivate;
            DamageBonus = damageBonus;
            Upgrade = upgrade;
            UpgradeLevel = upgradeLevel;
            Validate();
        }

        public static TurretInitializationData CreateDefault(
            TurretDefinition definition, bool isLocked = false, bool shouldActivate = false,
            int damageBonus = 0)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return new TurretInitializationData(
                definition, definition.MaxHealth, isLocked, shouldActivate, damageBonus);
        }

        // Revalidate immediately before use: ScriptableObject references are not immutable.
        internal void Validate()
        {
            if (Definition == null) throw new ArgumentNullException(nameof(Definition));
            if (string.IsNullOrWhiteSpace(Definition.Id) || Definition.MaxHealth < 1 || Definition.Power < 0)
                throw new ArgumentException("Invalid turret definition.", nameof(Definition));
            if (CurrentHealth < 0 || CurrentHealth > Definition.MaxHealth)
                throw new ArgumentOutOfRangeException(nameof(CurrentHealth));
            if (UpgradeLevel < 0 || UpgradeLevel > TurretRuntimeState.MaximumUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(UpgradeLevel));
            if (UpgradeLevel == 0)
            {
                if (Upgrade != null)
                    throw new ArgumentException("Rank zero must not select an upgrade.", nameof(Upgrade));
                return;
            }
            if (Upgrade == null || string.IsNullOrWhiteSpace(Upgrade.Id) ||
                !Upgrade.IsCompatibleWith(Definition.Id))
                throw new ArgumentException("Missing or incompatible upgrade.", nameof(Upgrade));
            if (!IsFinite(Upgrade.DamageModifierRatio) || !IsFinite(Upgrade.PowerModifierRatio) ||
                !IsFinite(Upgrade.RangeModifierRatio))
                throw new ArgumentException("Upgrade modifiers must be finite.", nameof(Upgrade));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

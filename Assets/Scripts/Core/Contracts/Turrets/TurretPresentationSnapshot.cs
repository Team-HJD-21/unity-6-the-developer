// 한 터렛의 읽기 전용 표시 상태입니다. 규칙/원본은 D에 남고 UI는 이 값을 수정하지 않습니다.
// 논리 ID로 대상을 찾으며 로컬 InstanceId나 MonoBehaviour를 다른 연결로 전달하지 않습니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class TurretPresentationSnapshot : IEquatable<TurretPresentationSnapshot>
    {
        public EntityId EntityId { get; }
        public DefinitionId DefinitionId { get; }
        public string DisplayName { get; }
        public BattlefieldPoint Position { get; }
        public int Level { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }
        public int Damage { get; }
        public int Power { get; }
        public double Range { get; }
        public double CoolingRemaining { get; }
        public DefinitionId SelectedUpgradeId { get; }
        public int UpgradeLevel { get; }
        public TurretPresentationFlags Flags { get; }
        public bool IsInitialized => Has(TurretPresentationFlags.Initialized);
        public bool IsActivated => Has(TurretPresentationFlags.Activated);
        public bool IsOperational => Has(TurretPresentationFlags.Operational);
        public bool IsDestroyed => Has(TurretPresentationFlags.Destroyed);
        public bool IsLocked => Has(TurretPresentationFlags.Locked);
        public bool IsManualOperationBlocked => Has(TurretPresentationFlags.ManualOperationBlocked);
        public bool IsCoolingDown => CoolingRemaining > 0;
        public bool CanModifyUpgrades => IsInitialized && !IsActivated && !IsDestroyed && !IsLocked && !IsManualOperationBlocked && !IsCoolingDown;
        public TurretPresentationSnapshot(EntityId entityId, DefinitionId definitionId, string displayName,
            BattlefieldPoint position, int level, int currentHealth, int maxHealth, int damage, int power,
            double range, double coolingRemaining, DefinitionId selectedUpgradeId, int upgradeLevel, TurretPresentationFlags flags)
        {
            if (entityId.IsEmpty || entityId.Value.Length > 128 || definitionId.IsEmpty || definitionId.Value.Length > 128)
                throw new ArgumentException("Provide bounded logical/definition IDs.");
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 256) throw new ArgumentException("Provide a display name.", nameof(displayName));
            if (level < 1 || currentHealth < 0 || maxHealth < currentHealth || damage < 0 || power < 0 || upgradeLevel < 0)
                throw new ArgumentException("Invalid turret presentation values.");
            if (double.IsNaN(range) || double.IsInfinity(range) || range < 0 ||
                double.IsNaN(coolingRemaining) || double.IsInfinity(coolingRemaining) || coolingRemaining < 0)
                throw new ArgumentException("Presentation distances/times must be finite and nonnegative.");
            if ((upgradeLevel == 0) != selectedUpgradeId.IsEmpty || (!selectedUpgradeId.IsEmpty && selectedUpgradeId.Value.Length > 128))
                throw new ArgumentException("Upgrade ID and level do not agree.");
            if (((int)flags & ~63) != 0) throw new ArgumentException("Unknown presentation flags.", nameof(flags));
            EntityId = entityId; DefinitionId = definitionId; DisplayName = displayName; Position = position; Level = level;
            CurrentHealth = currentHealth; MaxHealth = maxHealth; Damage = damage; Power = power; Range = range;
            CoolingRemaining = coolingRemaining; SelectedUpgradeId = selectedUpgradeId.IsEmpty ? default : selectedUpgradeId;
            UpgradeLevel = upgradeLevel; Flags = flags;
        }
        private bool Has(TurretPresentationFlags flag) => (Flags & flag) != 0;
        public bool Equals(TurretPresentationSnapshot other) => other != null && EntityId == other.EntityId &&
            DefinitionId == other.DefinitionId && DisplayName == other.DisplayName && Position == other.Position && Level == other.Level &&
            CurrentHealth == other.CurrentHealth && MaxHealth == other.MaxHealth && Damage == other.Damage && Power == other.Power &&
            Range == other.Range && CoolingRemaining == other.CoolingRemaining && SelectedUpgradeId == other.SelectedUpgradeId &&
            UpgradeLevel == other.UpgradeLevel && Flags == other.Flags;
        public override bool Equals(object obj) => obj is TurretPresentationSnapshot other && Equals(other);
        public override int GetHashCode() => EntityId.GetHashCode();
    }
}

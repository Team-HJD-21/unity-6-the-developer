// 개별 터렛의 저장 값과 콘텐츠 검증을 담당합니다. 로컬 InstanceId는 저장하지 않습니다.
// 실제 적용은 전력 예약을 함께 복원하는 Feature 소유자만 호출합니다.
using System;
using TeamHJD.Game.Content;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed partial class FieldTurretRuntimeState
    {
        /// <summary>Host의 터렛 writer가 현재 원본을 복사합니다. 초기화 전/피해 처리 중간 상태는 거부합니다.</summary>
        public State CaptureState(FieldTurretDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!_healthInitialized) throw new InvalidOperationException("The turret has not initialized its health.");
            return new State(definition.Id, definition.MaxHealth, _currentHealth, _isDestroyed,
                _isActivated, _isLocked, _isTemporarilySuspended, _damageBonus,
                SelectedUpgradeId, _upgradeLevel, _upgradeDamageModifierRatio, _upgradePowerModifierRatio,
                _upgradeRangeModifierRatio, _pendingRepairHealth);
        }

        // 개별 공개 복원은 전력 예산과 활성 예약을 분리시킬 수 있어 동일 Feature 내부에만 허용합니다.
        internal void RestoreState(State state, FieldTurretDefinition definition, FieldTurretUpgradeDefinition upgrade)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.ValidateFor(definition, upgrade);
            _currentHealth = state.CurrentHealth; _healthInitialized = true;
            _isDestroyed = state.IsDestroyed; _isActivated = state.IsActivated;
            _isLocked = state.IsLocked; _isTemporarilySuspended = state.IsTemporarilySuspended;
            _damageBonus = state.DamageBonus; _selectedUpgradeId = state.UpgradeId;
            _upgradeLevel = state.UpgradeLevel; _upgradeDamageModifierRatio = state.DamageModifierRatio;
            _upgradePowerModifierRatio = state.PowerModifierRatio; _upgradeRangeModifierRatio = state.RangeModifierRatio;
            _pendingRepairHealth = state.PendingRepairHealth;
        }

        /// <summary>명시 schema로 기록할 불변 값입니다. Unity 개체·전력 공급자·로컬 InstanceId를 포함하지 않습니다.</summary>
        public sealed class State
        {
            public string DefinitionId { get; }
            public int MaximumHealth { get; }
            public int CurrentHealth { get; }
            public bool IsDestroyed { get; }
            public bool IsActivated { get; }
            public bool IsLocked { get; }
            public bool IsTemporarilySuspended { get; }
            public int DamageBonus { get; }
            public string UpgradeId { get; }
            public int UpgradeLevel { get; }
            public float DamageModifierRatio { get; }
            public float PowerModifierRatio { get; }
            public float RangeModifierRatio { get; }
            public double PendingRepairHealth { get; }

            public State(string definitionId, int maximumHealth, int currentHealth, bool isDestroyed,
                bool isActivated, bool isLocked, bool isTemporarilySuspended, int damageBonus,
                string upgradeId, int upgradeLevel, float damageModifierRatio, float powerModifierRatio,
                float rangeModifierRatio, double pendingRepairHealth)
            {
                if (string.IsNullOrWhiteSpace(definitionId) || maximumHealth < 1 || currentHealth < 0 || currentHealth > maximumHealth ||
                    isDestroyed != (currentHealth == 0) || (isActivated && (isDestroyed || isLocked)) ||
                    (isTemporarilySuspended && !isActivated) || upgradeLevel < 0 || upgradeLevel > MaximumUpgradeLevel ||
                    !Finite(damageModifierRatio) || !Finite(powerModifierRatio) || !Finite(rangeModifierRatio) ||
                    double.IsNaN(pendingRepairHealth) || double.IsInfinity(pendingRepairHealth) || pendingRepairHealth < 0 || pendingRepairHealth >= 1 ||
                    ((isDestroyed || currentHealth == maximumHealth) && pendingRepairHealth != 0) ||
                    (upgradeLevel == 0 && (!string.IsNullOrEmpty(upgradeId) || damageModifierRatio != 0 || powerModifierRatio != 0 || rangeModifierRatio != 0)) ||
                    (upgradeLevel > 0 && string.IsNullOrWhiteSpace(upgradeId)))
                    throw new ArgumentException("Invalid saved turret runtime state.");
                DefinitionId = definitionId; MaximumHealth = maximumHealth; CurrentHealth = currentHealth;
                IsDestroyed = isDestroyed; IsActivated = isActivated; IsLocked = isLocked;
                IsTemporarilySuspended = isTemporarilySuspended; DamageBonus = damageBonus;
                UpgradeId = upgradeId ?? string.Empty; UpgradeLevel = upgradeLevel;
                DamageModifierRatio = damageModifierRatio; PowerModifierRatio = powerModifierRatio;
                RangeModifierRatio = rangeModifierRatio; PendingRepairHealth = pendingRepairHealth;
            }

            /// <summary>복원 준비 시 ID로 해석한 콘텐츠가 저장 정의·강화 계수와 같은지 상태 변경 전에 확인합니다.</summary>
            public void ValidateFor(FieldTurretDefinition definition, FieldTurretUpgradeDefinition upgrade)
            {
                if (definition == null || definition.Id != DefinitionId || definition.MaxHealth != MaximumHealth)
                    throw new ArgumentException("The saved turret definition does not match current content.", nameof(definition));
                _ = new FieldTurretInitializationData(definition, CurrentHealth, IsLocked, false, DamageBonus, upgrade, UpgradeLevel);
                if ((upgrade != null ? upgrade.Id : string.Empty) != UpgradeId ||
                    !MatchesModifier(upgrade != null ? upgrade.DamageModifierRatio : 0, UpgradeLevel, DamageModifierRatio) ||
                    !MatchesModifier(upgrade != null ? upgrade.PowerModifierRatio : 0, UpgradeLevel, PowerModifierRatio) ||
                    !MatchesModifier(upgrade != null ? upgrade.RangeModifierRatio : 0, UpgradeLevel, RangeModifierRatio))
                    throw new ArgumentException("The saved upgrade does not match current content.", nameof(upgrade));
            }

            // Mono의 중간 연산 정밀도 대신 실제 저장 필드와 같은 32비트 float로 비교합니다.
            private static bool MatchesModifier(float perRank, int rank, float saved) =>
                BitConverter.SingleToInt32Bits(perRank * rank) == BitConverter.SingleToInt32Bits(saved);
            private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

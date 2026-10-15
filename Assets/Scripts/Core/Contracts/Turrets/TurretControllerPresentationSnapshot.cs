// D가 소유하는 전력·CU·자동 활성화 상태의 UI용 읽기 전용 복사본입니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class TurretControllerPresentationSnapshot : IEquatable<TurretControllerPresentationSnapshot>
    {
        public int MaximumPower { get; }
        public int AvailablePower { get; }
        public int ReservedPower { get; }
        public int PendingRecoveryPower { get; }
        public int ControlUnitHealth { get; }
        public int ControlUnitMaxHealth { get; }
        public bool IsControlUnitAvailable { get; }
        public BattlefieldPoint ControlUnitPosition { get; }
        public bool IsAutoActivationRunning { get; }
        public double AutoActivationCooldown { get; }
        public RegionPolicyMode RegionMode { get; }
        public TurretControllerPresentationSnapshot(int maximumPower, int availablePower, int reservedPower, int pendingRecoveryPower,
            int controlUnitHealth, int controlUnitMaxHealth, BattlefieldPoint controlUnitPosition, bool isAutoActivationRunning,
            double autoActivationCooldown, RegionPolicyMode regionMode, bool isControlUnitAvailable = true)
        {
            if (maximumPower < 0 || availablePower < 0 || reservedPower < 0 || pendingRecoveryPower < 0 ||
                (long)availablePower + reservedPower + pendingRecoveryPower != maximumPower)
                throw new ArgumentException("Power presentation must preserve the D budget.");
            if (controlUnitHealth < 0 || controlUnitMaxHealth < controlUnitHealth) throw new ArgumentException("Invalid CU health.");
            if (double.IsNaN(autoActivationCooldown) || double.IsInfinity(autoActivationCooldown) || autoActivationCooldown < 0)
                throw new ArgumentOutOfRangeException(nameof(autoActivationCooldown));
            if (!Enum.IsDefined(typeof(RegionPolicyMode), regionMode)) throw new ArgumentOutOfRangeException(nameof(regionMode));
            MaximumPower = maximumPower; AvailablePower = availablePower; ReservedPower = reservedPower; PendingRecoveryPower = pendingRecoveryPower;
            ControlUnitHealth = controlUnitHealth; ControlUnitMaxHealth = controlUnitMaxHealth; ControlUnitPosition = controlUnitPosition;
            IsAutoActivationRunning = isAutoActivationRunning; AutoActivationCooldown = autoActivationCooldown; RegionMode = regionMode;
            IsControlUnitAvailable = isControlUnitAvailable;
        }
        public bool Equals(TurretControllerPresentationSnapshot other) => other != null && MaximumPower == other.MaximumPower &&
            AvailablePower == other.AvailablePower && ReservedPower == other.ReservedPower && PendingRecoveryPower == other.PendingRecoveryPower &&
            ControlUnitHealth == other.ControlUnitHealth && ControlUnitMaxHealth == other.ControlUnitMaxHealth &&
            ControlUnitPosition == other.ControlUnitPosition && IsAutoActivationRunning == other.IsAutoActivationRunning &&
            AutoActivationCooldown == other.AutoActivationCooldown && RegionMode == other.RegionMode && IsControlUnitAvailable == other.IsControlUnitAvailable;
        public override bool Equals(object obj) => obj is TurretControllerPresentationSnapshot other && Equals(other);
        public override int GetHashCode() => MaximumPower.GetHashCode();
    }
}

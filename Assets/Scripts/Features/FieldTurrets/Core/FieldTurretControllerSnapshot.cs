using System.Collections.Generic;
using System.Collections.ObjectModel;
using TeamHJD.Game.FieldTurrets.Contracts;

namespace TeamHJD.Game.FieldTurrets
{
    /// <summary>
    /// 한 컨트롤러의 조회 시점 전력과 등록 터렛 집계 복사본.
    /// 컨트롤러와 터렛 및 변경 가능한 원본 컬렉션 참조를 노출하지 않는다.
    /// </summary>
    public sealed class FieldTurretControllerSnapshot
    {
        public int MaximumPower { get; }
        public int AvailablePower { get; }
        public int ReservedPower { get; }
        public int PendingRecoveryPower { get; }
        public int RegisteredTurretCount { get; }
        public int ActivatedTurretCount { get; }
        public int OperationalTurretCount { get; }
        public IReadOnlyDictionary<FieldTurretKind, int> ActivatedCountByType { get; }

        internal FieldTurretControllerSnapshot(
            int maximumPower, int availablePower, int reservedPower, int pendingRecoveryPower,
            int registeredTurretCount, int activatedTurretCount, int operationalTurretCount,
            IDictionary<FieldTurretKind, int> activatedCountByType)
        {
            MaximumPower = maximumPower;
            AvailablePower = availablePower;
            ReservedPower = reservedPower;
            PendingRecoveryPower = pendingRecoveryPower;
            RegisteredTurretCount = registeredTurretCount;
            ActivatedTurretCount = activatedTurretCount;
            OperationalTurretCount = operationalTurretCount;
            ActivatedCountByType = new ReadOnlyDictionary<FieldTurretKind, int>(
                new Dictionary<FieldTurretKind, int>(activatedCountByType));
        }

        public int GetActivatedCount(FieldTurretKind kind) =>
            ActivatedCountByType.TryGetValue(kind, out int count) ? count : 0;
    }
}

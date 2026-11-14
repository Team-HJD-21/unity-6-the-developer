// 한 Mission 실행에서 사용한 구조 시간과 안전장치 횟수입니다. 월드 rollback 대상이 아닙니다.
// 새 Match ID 발급으로 초기화하지 않으며 명시적인 새 Mission만 새 실행을 만듭니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class MissionRecoveryLedger
    {
        public const int MaximumSafetyUses = 3;
        public const double InitialRescueSeconds = 60;
        public Guid RunId { get; private set; } = Guid.NewGuid();
        public double RescueSeconds { get; private set; } = InitialRescueSeconds;
        public int SafetyUses { get; private set; }
        public Guid PendingFailure { get; private set; }
        public bool IsEnded { get; private set; }
        public CheckpointSlot LastCheckpoint { get; private set; }
        public Guid LastCheckpointId { get; private set; }
        public event Action RescueTimeChanged;
        public event Action CriticalChanged;
        public void BeginNewMission()
        {
            RunId = Guid.NewGuid(); RescueSeconds = InitialRescueSeconds; SafetyUses = 0; PendingFailure = Guid.Empty; IsEnded = false; LastCheckpoint = default; LastCheckpointId = Guid.Empty;
            CriticalChanged?.Invoke();
        }
        public void SpendRescueTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (seconds == 0 || RescueSeconds == 0 || PendingFailure != Guid.Empty) return;
            RescueSeconds = Math.Max(0, RescueSeconds - seconds); RescueTimeChanged?.Invoke();
        }
        /// <summary>같은 실패의 기술적 재시도는 추가 차감하지 않습니다. false면 사용 가능 횟수가 없습니다.</summary>
        public bool TryBeginFailure(Guid failure)
        {
            if (failure == Guid.Empty) throw new ArgumentException("A failure identity is required.", nameof(failure));
            if (IsEnded) return false;
            if (PendingFailure != Guid.Empty) return PendingFailure == failure;
            if (SafetyUses >= MaximumSafetyUses) { EndMission(); return false; }
            SafetyUses++; PendingFailure = failure; CriticalChanged?.Invoke(); return true;
        }
        public void CompleteRecovery(Guid failure)
        {
            if (failure == Guid.Empty || PendingFailure != failure) throw new InvalidOperationException("Complete only the pending recovery.");
            PendingFailure = Guid.Empty; CriticalChanged?.Invoke();
        }
        public void RecordCheckpoint(CheckpointSlot slot, Guid checkpoint)
        {
            if (slot.IsEmpty || checkpoint == Guid.Empty) throw new ArgumentException("A committed checkpoint is required.");
            if (LastCheckpoint.Equals(slot) && LastCheckpointId == checkpoint) return;
            LastCheckpoint = slot; LastCheckpointId = checkpoint; CriticalChanged?.Invoke();
        }
        public void EndMission()
        {
            if (IsEnded) return;
            IsEnded = true; PendingFailure = Guid.Empty; CriticalChanged?.Invoke();
        }
        public void FlushChanges() => CriticalChanged?.Invoke();
        public void Restore(Guid run, double rescueSeconds, int safetyUses, Guid pendingFailure, bool ended = false, CheckpointSlot lastCheckpoint = default, Guid checkpoint = default)
        {
            if (run == Guid.Empty || double.IsNaN(rescueSeconds) || double.IsInfinity(rescueSeconds) || rescueSeconds < 0 ||
                rescueSeconds > InitialRescueSeconds || safetyUses < 0 || safetyUses > MaximumSafetyUses ||
                (pendingFailure != Guid.Empty && (safetyUses == 0 || ended)) || (lastCheckpoint.IsEmpty != (checkpoint == Guid.Empty))) throw new ArgumentException("Invalid Mission recovery ledger.");
            RunId = run; RescueSeconds = rescueSeconds; SafetyUses = safetyUses; PendingFailure = pendingFailure; IsEnded = ended; LastCheckpoint = lastCheckpoint; LastCheckpointId = checkpoint;
        }
    }
}

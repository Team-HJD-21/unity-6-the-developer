// 공용 전력 예산의 불변 저장 값과 지연 반환 진행량 복원을 제공합니다.
// 터렛 전체 예약 합계와의 검증은 Feature 소유자가 적용 전에 수행합니다.
using System;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed partial class FieldTurretPowerBudget
    {
        /// <summary>Host writer가 가용·예약·반환 대기 전력과 다음 반환까지의 누적 시간을 복사합니다.</summary>
        public State CaptureState() => new(MaximumPower, CurrentPower, PendingRecovery, _recoveryElapsed);

        /// <summary>동일 Feature의 전체 복원 단계에서 사용합니다. 신규 소비/반환 이벤트를 재실행하지 않습니다.</summary>
        internal void RestoreState(State state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            MaximumPower = state.Maximum; CurrentPower = state.Current;
            PendingRecovery = state.PendingRecovery; _recoveryElapsed = state.RecoveryElapsed;
        }

        // 전체 개체와 예약의 적용이 끝난 뒤 한 번만 화면의 읽기 모델을 갱신합니다.
        internal void PublishRestoredState() => NotifyChanged();

        public sealed class State
        {
            public int Maximum { get; }
            public int Current { get; }
            public int PendingRecovery { get; }
            public int Reserved => Maximum - Current - PendingRecovery;
            public double RecoveryElapsed { get; }
            public State(int maximum, int current, int pendingRecovery, double recoveryElapsed)
            {
                if (maximum < 0 || current < 0 || current > maximum || pendingRecovery < 0 || pendingRecovery > maximum - current ||
                    double.IsNaN(recoveryElapsed) || double.IsInfinity(recoveryElapsed) || recoveryElapsed < 0 || recoveryElapsed >= RecoveryInterval ||
                    (pendingRecovery == 0 && recoveryElapsed != 0))
                    throw new ArgumentException("Invalid saved turret power budget.");
                Maximum = maximum; Current = current; PendingRecovery = pendingRecovery; RecoveryElapsed = recoveryElapsed;
            }
        }
    }
}

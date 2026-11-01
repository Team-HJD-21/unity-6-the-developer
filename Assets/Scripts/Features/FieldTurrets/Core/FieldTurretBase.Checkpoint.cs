// 실제 터렛의 공통 배치·냉각·발사 진행·시작 의도를 저장하고 Feature의 전체 복원 안에서 적용합니다.
// 강화·활성화·피해 명령을 재실행하지 않으며 전력 예약은 공용 예산과 함께 교체합니다.
using System;
using TeamHJD.Game.Content;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    public abstract partial class FieldTurretBase
    {
        /// <summary>Host의 Feature writer가 정지된 일관된 프레임에서 공통 원본을 복사합니다. 종류별 전투 상태는 별도입니다.</summary>
        public FieldTurretActorState CaptureActorState()
        {
            if (!IsInitialized || !isActiveAndEnabled || !HasStateAuthority || !TryGetEntityId(out var id))
                throw new InvalidOperationException("An initialized authority turret with a logical ID is required.");
            if (_activationController.ReservedPower != (IsActivated ? EffectivePower : 0))
                throw new InvalidOperationException("The turret's actual power reservation is inconsistent.");
            return new FieldTurretActorState(id, _runtimeState.CaptureState(_definition), CheckpointTransformState.Capture(transform),
                turretRotationPoint != null, turretRotationPoint != null ? turretRotationPoint.localRotation : Quaternion.identity,
                TimeTilFire, Math.Min(_coolingDuration, Math.Max(0, _coolingEndsAt - FieldSeconds)),
                _coolingDuration, _coolingInitialHeatRatio, _initialActivationPending);
        }

        /// <summary>Feature의 준비 단계가 해석한 콘텐츠와 제작 계층을 개체 변경 전에 확인합니다.</summary>
        public void ValidateActorState(FieldTurretActorState state, FieldTurretUpgradeDefinition upgrade)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.Runtime.ValidateFor(_definition, upgrade);
            if (state.HasHead != (turretRotationPoint != null))
                throw new ArgumentException("The saved turret head does not match the authored prefab.", nameof(state));
        }

        internal int CheckpointReservedPower => _activationController?.ReservedPower ?? 0;

        // actor 교체 전 예약 반환/자동 시작을 끊습니다. 예산 전체가 뒤에서 저장 값으로 교체됩니다.
        internal void DetachCheckpointReservation()
        { CancelInitialActivation(); _activationController?.DetachReservationForCheckpoint(); }

        internal void RestoreActorState(FieldTurretActorState state, FieldTurretUpgradeDefinition upgrade)
        {
            ValidateActorState(state, upgrade);
            if (!IsInitialized || !HasStateAuthority || !TryGetEntityId(out var id) || id != state.EntityId)
                throw new InvalidOperationException("Restore requires the current authority actor and the saved logical ID.");
            DetachCheckpointReservation();
            _runtimeState.RestoreState(state.Runtime, _definition, upgrade);
            _activationController.RestoreReservation(EffectivePower);
            state.Pose.Apply(transform);
            if (turretRotationPoint != null) turretRotationPoint.localRotation = state.HeadLocalRotation;
            TimeTilFire = state.FireElapsed;
            _coolingDuration = state.CoolingDuration; _coolingInitialHeatRatio = state.CoolingInitialHeatRatio;
            _coolingEndsAt = FieldSeconds + state.CoolingRemaining;
            LastInitialActivationResult = null;
            _initialActivationPending = state.PendingInitialActivation;
            if (_initialActivationPending) _initialActivationRoutine = StartCoroutine(RequestInitialActivation());
            RefreshRangeVisual();
            // 실제 종류별 열/사격 상태는 같은 Feature의 전투 상태 적용이 이어서 복원합니다.
        }

        internal void PublishCheckpointState() => FieldTurretInstanceRegistry.NotifySnapshotChanged(this);
    }
}

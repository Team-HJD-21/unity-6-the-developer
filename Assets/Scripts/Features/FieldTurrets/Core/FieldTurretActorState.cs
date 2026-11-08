// 터렛 actor의 공통 원본을 불변 값으로 보존합니다. 콘텐츠와 논리 ID를 로컬 InstanceId와 구분합니다.
// 종류별 표적·열 누적·투사체는 해당 전투 Feature가 별도로 보존합니다.
using System;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed class FieldTurretActorState
    {
        public EntityId EntityId { get; }
        public FieldTurretRuntimeState.State Runtime { get; }
        public CheckpointTransformState Pose { get; }
        public bool HasHead { get; }
        public Quaternion HeadLocalRotation { get; }
        public float FireElapsed { get; }
        public double CoolingRemaining { get; }
        public float CoolingDuration { get; }
        public float CoolingInitialHeatRatio { get; }
        public bool PendingInitialActivation { get; }
        public int ReservedPower { get; }

        public FieldTurretActorState(EntityId entityId, FieldTurretRuntimeState.State runtime,
            CheckpointTransformState pose, bool hasHead, Quaternion headLocalRotation, float fireElapsed,
            double coolingRemaining, float coolingDuration, float coolingInitialHeatRatio, bool pendingInitialActivation, int reservedPower = 0)
        {
            if (entityId.IsEmpty) throw new ArgumentException("A saved logical turret ID is required.", nameof(entityId));
            if (reservedPower < 0) throw new ArgumentOutOfRangeException(nameof(reservedPower));
            ReservedPower = reservedPower;
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _ = new CheckpointTransformState(pose.Position, pose.Rotation, pose.LocalScale);
            if (!Finite(headLocalRotation.x) || !Finite(headLocalRotation.y) || !Finite(headLocalRotation.z) || !Finite(headLocalRotation.w) ||
                Math.Abs(Quaternion.Dot(headLocalRotation, headLocalRotation) - 1f) > .001f ||
                !Finite(fireElapsed) || fireElapsed < 0 || !Finite(coolingDuration) || coolingDuration < 0 ||
                double.IsNaN(coolingRemaining) || double.IsInfinity(coolingRemaining) || coolingRemaining < 0 || coolingRemaining > coolingDuration ||
                !Finite(coolingInitialHeatRatio) || coolingInitialHeatRatio < 0 || coolingInitialHeatRatio > 1 ||
                (runtime.IsTemporarilySuspended && coolingDuration <= 0) ||
                (pendingInitialActivation && (runtime.IsActivated || runtime.IsDestroyed || runtime.IsLocked)))
                throw new ArgumentException("Invalid saved turret actor state.");
            EntityId = entityId; Pose = pose; HasHead = hasHead; HeadLocalRotation = headLocalRotation;
            FireElapsed = fireElapsed; CoolingRemaining = coolingRemaining; CoolingDuration = coolingDuration;
            CoolingInitialHeatRatio = coolingInitialHeatRatio; PendingInitialActivation = pendingInitialActivation;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

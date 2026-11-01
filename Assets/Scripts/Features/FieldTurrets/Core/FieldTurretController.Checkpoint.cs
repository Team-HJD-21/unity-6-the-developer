// 실제 소유 터렛들의 예약 합계와 공용 예산을 검증하고 자동 활성화의 남은 실행을 연결합니다.
// 전체 writer가 actor·전투 상태를 재구성한 뒤 이 내부 적용을 호출합니다.
using System;
using System.Collections.Generic;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed partial class FieldTurretController
    {
        /// <summary>Host writer가 실제 예약과 예산이 일치하는 프레임에서 CU 큐와 남은 시간을 복사합니다.</summary>
        public FieldTurretControllerState CaptureControllerState()
        {
            if (!HasWriteAuthority || !isActiveAndEnabled) throw new InvalidOperationException("A current authority turret owner is required.");
            var actors = ReadCheckpointActors(); long reserved = 0;
            foreach (var actor in actors.Values)
            { _ = actor.CaptureActorState(); reserved += actor.CheckpointReservedPower; }
            if (reserved != Budget.ReservedPower)
                throw new InvalidOperationException("The shared budget contains unaccounted turret reservations.");
            var candidates = new List<FieldTurretControllerState.Candidate>();
            foreach (var candidate in _autoCandidates)
                candidates.Add(new FieldTurretControllerState.Candidate(candidate.EntityId, candidate.EnemyCount, candidate.Score, candidate.Completed));
            return new FieldTurretControllerState(Budget.CaptureState(), Math.Min(AutoActivationCooldownSeconds,
                Math.Max(0, _nextAutoActivationTime - FieldSeconds)), IsAutoActivationRunning, candidates);
        }

        internal void RestoreControllerState(FieldTurretControllerState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!HasWriteAuthority || !isActiveAndEnabled) throw new InvalidOperationException("A current authority turret owner is required.");
            var actors = ReadCheckpointActors(); long reserved = 0;
            foreach (var actor in actors.Values)
            { _ = actor.CaptureActorState(); reserved += actor.CheckpointReservedPower; }
            if (reserved != state.Budget.Reserved)
                throw new ArgumentException("Saved power does not match the recreated turret reservations.", nameof(state));
            var queue = new List<AutoActivationCandidate>();
            foreach (var saved in state.Candidates)
            {
                bool exists = actors.TryGetValue(saved.EntityId, out var actor);
                if (!exists && !saved.Completed) throw new ArgumentException("A pending auto activation target is missing.", nameof(state));
                queue.Add(new AutoActivationCandidate(exists ? actor.InstanceId : 0, saved.EnemyCount, saved.Score, saved.EntityId)
                { Completed = saved.Completed });
            }
            // 모든 값/대응 검증 뒤에만 실제 원본을 바꿉니다. 새 ON/OFF 요청은 발생하지 않습니다.
            Budget.RestoreState(state.Budget); maximumPower = state.Budget.Maximum;
            _autoCandidates.Clear(); _autoCandidates.AddRange(queue);
            _nextAutoActivationTime = FieldSeconds + state.AutoActivationRemaining;
            IsAutoActivationRunning = state.AutoActivationRunning;
        }

        internal void PublishCheckpointState()
        {
            Budget.PublishRestoredState();
            foreach (var actor in ReadCheckpointActors().Values) actor.PublishCheckpointState();
        }

        private Dictionary<EntityId, FieldTurretBase> ReadCheckpointActors()
        {
            var actors = new Dictionary<EntityId, FieldTurretBase>();
            foreach (var actor in FieldTurretInstanceRegistry.RegisteredInstances.Values)
            {
                if (!OwnsTurret(actor)) continue;
                if (!actor.TryGetEntityId(out var id) || !actors.TryAdd(id, actor))
                    throw new InvalidOperationException("Every owned turret needs a unique logical ID.");
            }
            return actors;
        }
    }
}

// 공용 전력과 CU 자동 활성화 큐의 불변 저장 값입니다.
// 지역 정의는 Core 지역 writer가 소유하며, 이 큐는 로컬 InstanceId 대신 논리 터렛 ID를 사용합니다.
using System;
using System.Collections.Generic;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed class FieldTurretControllerState
    {
        public const int MaximumCandidates = 4096;
        public FieldTurretPowerBudget.State Budget { get; }
        public double AutoActivationRemaining { get; }
        public bool AutoActivationRunning { get; }
        public IReadOnlyList<Candidate> Candidates { get; }
        public FieldTurretControllerState(FieldTurretPowerBudget.State budget, double autoActivationRemaining,
            bool autoActivationRunning, IEnumerable<Candidate> candidates)
        {
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
            if (double.IsNaN(autoActivationRemaining) || double.IsInfinity(autoActivationRemaining) || autoActivationRemaining < 0 ||
                autoActivationRemaining > FieldTurretController.AutoActivationCooldownSeconds)
                throw new ArgumentOutOfRangeException(nameof(autoActivationRemaining));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var copy = new List<Candidate>(); var ids = new HashSet<EntityId>(); bool pending = false;
            foreach (var candidate in candidates)
            {
                if (candidate == null || copy.Count >= MaximumCandidates || !ids.Add(candidate.EntityId))
                    throw new ArgumentException("Saved auto activation candidates must be bounded and unique.", nameof(candidates));
                copy.Add(candidate); pending |= !candidate.Completed;
            }
            if (autoActivationRunning && !pending) throw new ArgumentException("A running auto activation queue needs a pending candidate.");
            AutoActivationRemaining = autoActivationRemaining; AutoActivationRunning = autoActivationRunning;
            Candidates = copy.AsReadOnly();
        }

        public sealed class Candidate
        {
            public EntityId EntityId { get; }
            public int EnemyCount { get; }
            public float Score { get; }
            public bool Completed { get; }
            public Candidate(EntityId entityId, int enemyCount, float score, bool completed)
            {
                if (entityId.IsEmpty || enemyCount < 0 || float.IsNaN(score) || float.IsInfinity(score))
                    throw new ArgumentException("Invalid saved auto activation candidate.");
                EntityId = entityId; EnemyCount = enemyCount; Score = score; Completed = completed;
            }
        }
    }
}

// Planner가 확정한 생성 명령과 선택 근거를 Executor에 전달합니다.
// 계획 수량은 실제 생성 수량이 아니며, 실제 결과만 생성 이력에 기록합니다.
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Enemies.Encounter
{
    internal sealed class EncounterPlan
    {
        public BattlefieldSpatialSnapshot Battlefield { get; }
        public IReadOnlyList<SpawnInstruction> Instructions { get; }
        public IReadOnlyList<SpawnDecisionSnapshot.Candidate> Candidates { get; }
        public string Policy { get; }
        public string EnvironmentPolicy { get; }
        public double PlanningMilliseconds { get; }

        public EncounterPlan(BattlefieldSpatialSnapshot battlefield, IReadOnlyList<SpawnInstruction> instructions,
            IReadOnlyList<SpawnDecisionSnapshot.Candidate> candidates, string policy, string environmentPolicy, double milliseconds)
        {
            Battlefield = battlefield;
            Instructions = instructions;
            Candidates = candidates;
            Policy = policy;
            EnvironmentPolicy = environmentPolicy;
            PlanningMilliseconds = milliseconds;
        }
    }
}

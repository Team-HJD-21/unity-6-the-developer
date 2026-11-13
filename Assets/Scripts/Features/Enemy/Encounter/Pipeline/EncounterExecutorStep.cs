// Planner가 만든 명령을 실제 생성기에 전달하고 계획/실제 결과를 구분합니다.
// 명령마다 최신 점유를 재검사하며, 일부 생성 뒤의 실패를 전체 재실행하지 않습니다.
using System;
using System.Collections.Generic;
using System.Text;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Enemies.Encounter
{
    internal sealed class EncounterExecutorStep
    {
        private readonly EnemySpawnExecutor _spawnExecutor;
        private readonly Func<RegionGridSnapshot> _region;
        private static readonly Unity.Profiling.ProfilerMarker SpawnMarker = new Unity.Profiling.ProfilerMarker("TeamHJD.Encounter.SpawnAndCommit");
        public EncounterExecutorStep(EnemySpawnExecutor executor, Func<RegionGridSnapshot> region)
        { _spawnExecutor = executor; _region = region; }
        public bool Execute(EncounterCollection collection, EncounterPlan plan, EncounterExecutionContext context,
            double margin, out string result, out EncounterExecutionSummary summary,
            out IReadOnlyList<SpawnDecisionSnapshot.Actual> executed)
        {
            var snapshot = plan.Battlefield;
            var instructions = plan.Instructions;
            var executionSummary = new StringBuilder();
            int spawnedCount = 0;
            int plannedCount = 0;
            var plans = new List<SpawnDecisionSnapshot.Plan>();
            var actuals = new List<SpawnDecisionSnapshot.Actual>();
            executed = actuals.AsReadOnly();
            var spawnClock = System.Diagnostics.Stopwatch.StartNew();
            using (SpawnMarker.Auto())
            {
            foreach (SpawnInstruction instruction in instructions)
            {
                foreach (EnemySpawnEntry entry in instruction.Enemies)
                {
                    plannedCount += entry.Count;
                }
                // 계획 뒤 점유가 바뀔 수 있어 명령마다 생성 직전의 확정 지역을 다시 읽습니다.
                var latestRegion = _region == null ? collection.Region : _region();
                if (latestRegion == null || latestRegion.MatchId != snapshot.MatchId)
                    throw new InvalidOperationException("The active region belongs to another Match.");
                collection.Points.ApplyRegionSnapshot(latestRegion, margin);
                string failure = "";
                int instructionSpawned = _spawnExecutor.Execute(instruction, context, actuals.Add,reason=>failure=reason, snapshot);
                foreach(EnemySpawnEntry entry in instruction.Enemies)
                    plans.Add(new SpawnDecisionSnapshot.Plan(instruction.SpawnPointId,entry.EnemyId,entry.Count,failure));
                spawnedCount += instructionSpawned;
                if (executionSummary.Length > 0) executionSummary.Append(" | ");
                executionSummary.Append(instruction.SquadOrder).Append(" @ ").Append(instruction.SpawnPointId).Append(": ");
                for (int index = 0; index < instruction.Enemies.Count; index++)
                {
                    if (index > 0) executionSummary.Append(", ");
                    EnemySpawnEntry entry = instruction.Enemies[index];
                    executionSummary.Append(entry.EnemyId).Append(" planned ").Append(entry.Count);
                }
                executionSummary.Append(" / spawned ").Append(instructionSpawned);
            }


            }
            spawnClock.Stop();
            result = $"Match {snapshot.MatchId} r{snapshot.Revision} → Encounter: " +
                     $"{snapshot.Triangles.Count} triangles, {snapshot.FrontlineEdges.Count} frontline edges, " +
                     $"{snapshot.Grid.OccupiedCells.Count} occupied cells; " +
                     $"{instructions.Count} plan(s), {spawnedCount} spawned. {executionSummary} " +
                     $"Environment r{context?.Environment?.Revision ?? 0}, policy={plan.EnvironmentPolicy}";
            summary = new EncounterExecutionSummary(plannedCount, spawnedCount,
                spawnedCount == plannedCount ? "Spawned" : spawnedCount > 0 ? "PartialSpawn" : "NoSpawn",
                new SpawnDecisionSnapshot(snapshot,plan.Candidates,plans,actuals,
                    spawnedCount == plannedCount ? "Spawned" : spawnedCount > 0 ? "PartialSpawn" : "NoSpawn",
                    plan.Policy, context?.Environment, plan.EnvironmentPolicy, context?.Placement,
                    plan.PlanningMilliseconds, spawnClock.Elapsed.TotalMilliseconds));
            return spawnedCount > 0;
        }
    }
}

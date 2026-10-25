// 최신 Battlefield snapshot을 Spawn Planner에 전달하고 실행 결과를 보고합니다.
// 실제 NGO 생성은 Scene의 EnemySpawnExecutor에 위임합니다.

using System.Collections.Generic;
using System.Text;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;
using UnityEngine;

namespace TeamHJD.Game.Domain
{
    /// <summary>
    /// Match 공간 정보를 Planner에 전달하고 분대 생성 결과를 보고한다.
    /// 스폰 계획은 Planner에, 실제 네트워크 생성은 Executor에 위임한다.
    /// </summary>
    public class EncounterRuntime
    {
        private readonly SpawnCompositionPlanner _planner;
        private readonly EnemySpawnExecutor _spawnExecutor;

        /// <summary>
        /// 분대 프리셋 카탈로그가 로드되어 계획을 세울 수 있는지 반환한다.
        /// </summary>
        public bool HasCatalog => _planner.HasCatalog;

        /// <summary>
        /// 게임 시작 시 씬에서 Executor를 찾았는지 반환한다.
        /// </summary>
        public bool HasExecutor => _spawnExecutor != null;
        public bool CanExecute => _spawnExecutor != null && _spawnExecutor.CanSpawn;
        public EnemySpawnExecutor Executor => _spawnExecutor;
        
        /// <summary>
        /// 기본 Planner를 준비하고 현재 씬의 Executor를 한 번 찾아 보관한다.
        /// </summary>
        public EncounterRuntime() : this(FindStandaloneExecutor())
        {
        }

        public EncounterRuntime(EnemySpawnExecutor executor)
        {
            _planner = new SpawnCompositionPlanner();
            _spawnExecutor = executor;
        }

        private static EnemySpawnExecutor FindStandaloneExecutor()
        {
            EnemySpawnExecutor selected = null;
            foreach (var candidate in Object.FindObjectsByType<EnemySpawnExecutor>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) continue;
                if (selected != null) throw new System.InvalidOperationException("Assign an explicit Enemy executor when the Scene contains multiple executors.");
                selected = candidate;
            }
            return selected;
        }

        /// <summary>
        /// Planner가 결정한 지점별 명령을 Executor에 전달해 적을 생성한다.
        /// 임시 분대 유형과 생성 상한은 Planner가 결정한다.
        /// </summary>
        public void Spawn()
        {
            if (_spawnExecutor == null)
            {
                Debug.LogError("Enemy spawn executor was not found.");
                return;
            }

            if (!_planner.TryPlan(_spawnExecutor.SpawnPointList,
                    out IReadOnlyList<SpawnInstruction> instructions))
            {
                Debug.LogWarning("No spawn plan: check the squad preset, budget, and available spawn points.");
                return;
            }

            // Planner는 여러 지점의 명령을 반환할 수 있으므로 각각 실행한다.
            foreach (SpawnInstruction instruction in instructions)
                _spawnExecutor.Execute(instruction);
        }

        /// <summary>
        /// Match 공간 snapshot을 Encounter 경계로 받아 요청한 PoC 분대를 계획하고 서버 Executor에 전달합니다.
        /// Grid의 점유·적 밀도와 Frontline 거리를 PoC 배치 정책에 적용합니다.
        /// </summary>
        public bool TrySpawn(
            BattlefieldSpatialSnapshot snapshot,
            string squadOrder,
            int maxEnemyCount,
            out string result)
            => TrySpawn(snapshot, squadOrder, maxEnemyCount, null, out result, out _);

        public bool TrySpawn(BattlefieldSpatialSnapshot snapshot, string squadOrder, int maxEnemyCount,
            EncounterExecutionContext context, out string result, out EncounterExecutionSummary summary, EncounterEnvironmentProfile environmentProfile = null)
        {
            summary = new EncounterExecutionSummary(0, 0, "Rejected");
            if (snapshot == null || snapshot.MatchId.IsEmpty)
            {
                result = "활성 Match Battlefield snapshot이 없습니다.";
                return false;
            }

            if (_spawnExecutor == null)
            {
                result = "현재 Scene에서 EnemySpawnExecutor를 찾지 못했습니다.";
                return false;
            }

            if (!_spawnExecutor.CanSpawn)
            {
                result = "Encounter 실행에는 NGO Host/Server가 필요합니다. NetworkManager 상태를 확인하세요.";
                return false;
            }

            if (context != null && context.SpawnCancellation.IsCancellationRequested)
            {
                result = "The Match/source no longer accepts pending spawns.";
                summary = new EncounterExecutionSummary(0, 0, "OriginUnavailable"); return false;
            }
            if (context?.Placement != null && context.Placement.Origin.Delivery != SpawnDeliveryMode.Immediate)
            {
                result = "Ground emergence / air drop transitions require an explicit Enemy delivery adapter.";
                summary = new EncounterExecutionSummary(0, 0, "UnsupportedDelivery"); return false;
            }

            if (string.IsNullOrWhiteSpace(squadOrder) || maxEnemyCount <= 0)
            {
                result = "분대 명령과 최대 생성 수를 확인하세요.";
                return false;
            }

            var request = new SpawnRequest(squadOrder.Trim(), maxEnemyCount);
            IReadOnlyList<SpawnInstruction> instructions;
            bool planned = context?.Placement == null
                ? _planner.TryPlan(request, _spawnExecutor.SpawnPointList, snapshot, context?.Environment, environmentProfile, out instructions)
                : _planner.TryPlanOrigin(request, _spawnExecutor.SpawnPointList, snapshot, context.Environment, environmentProfile, context.Placement, out instructions);
            if (!planned)
            {
                result = $"SpawnPlan 생성 실패: preset '{request.SquadOrder}', budget {request.MaxCount}, " +
                         $"available points {_spawnExecutor.SpawnPointList.GetAvailableSpawnPoints().Count}. " +
                         "Grid 밖 또는 Player/Turret 점유 셀의 SpawnPoint는 제외됩니다.";
                summary = new EncounterExecutionSummary(0,0,"PlanRejected", new SpawnDecisionSnapshot(snapshot,
                    _planner.LastCandidates,System.Array.Empty<SpawnDecisionSnapshot.Plan>(),System.Array.Empty<SpawnDecisionSnapshot.Actual>(),result,
                    SpawnPointSpatialPolicy.Description, context?.Environment, _planner.LastEnvironmentPolicy, context?.Placement));
                return false;
            }

            var executionSummary = new StringBuilder();
            int spawnedCount = 0;
            int plannedCount = 0;
            var plans = new List<SpawnDecisionSnapshot.Plan>();
            var actuals = new List<SpawnDecisionSnapshot.Actual>();
            foreach (SpawnInstruction instruction in instructions)
            {
                foreach (EnemySpawnEntry entry in instruction.Enemies)
                {
                    plannedCount += entry.Count;
                }
                string failure="";
                int instructionSpawned = _spawnExecutor.Execute(instruction, context, actuals.Add,reason=>failure=reason, snapshot);
                foreach(EnemySpawnEntry entry in instruction.Enemies)
                    plans.Add(new SpawnDecisionSnapshot.Plan(instruction.SpawnPointId,entry.EnemyId,entry.Count,failure));
                spawnedCount += instructionSpawned;
                if (executionSummary.Length > 0) executionSummary.Append(" | ");
                executionSummary.Append(instruction.SpawnPointId).Append(": ");
                for (int index = 0; index < instruction.Enemies.Count; index++)
                {
                    if (index > 0) executionSummary.Append(", ");
                    EnemySpawnEntry entry = instruction.Enemies[index];
                    executionSummary.Append(entry.EnemyId).Append(" planned ").Append(entry.Count);
                }
                executionSummary.Append(" / spawned ").Append(instructionSpawned);
            }

            _planner.RecordExecuted(snapshot, actuals);
            result = $"Match {snapshot.MatchId} r{snapshot.Revision} → Encounter: " +
                     $"{snapshot.Triangles.Count} triangles, {snapshot.FrontlineEdges.Count} frontline edges, " +
                     $"{snapshot.Grid.OccupiedCells.Count} occupied cells; " +
                     $"{instructions.Count} plan(s), {spawnedCount} spawned. {executionSummary} " +
                     $"Environment r{context?.Environment?.Revision ?? 0}, policy={_planner.LastEnvironmentPolicy}";
            Debug.Log(result);
            summary = new EncounterExecutionSummary(plannedCount, spawnedCount,
                spawnedCount == plannedCount ? "Spawned" : spawnedCount > 0 ? "PartialSpawn" : "NoSpawn",
                new SpawnDecisionSnapshot(snapshot,_planner.LastCandidates,plans,actuals,
                    spawnedCount == plannedCount ? "Spawned" : spawnedCount > 0 ? "PartialSpawn" : "NoSpawn",
                    SpawnPointSpatialPolicy.Description, context?.Environment, _planner.LastEnvironmentPolicy, context?.Placement));
            return spawnedCount > 0;
        }
    }
}


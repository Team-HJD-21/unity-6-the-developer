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
        /// 분대 프리셋과 몬스터 카탈로그가 준비되어 계획을 세울 수 있는지 반환한다.
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
        public EncounterRuntime()
        {
            _spawnExecutor = Object.FindAnyObjectByType<EnemySpawnExecutor>();
            _planner = new SpawnCompositionPlanner(
                EnemySquadPresetCatalog.Load(),
                _spawnExecutor != null ? _spawnExecutor.Catalog : null);
        }

        /// <summary>
        /// Planner가 결정한 지점별 명령을 Executor에 전달해 적을 생성한다.
        /// 호출자가 전달한 분대 명령과 생성 상한, 비용 예산으로 계획한다.
        /// </summary>
        public void RequestSpawn(SpawnRequest request)
        {
            if (_spawnExecutor == null)
            {
                Debug.LogError("Enemy spawn executor was not found.");
                return;
            }

            if (!_planner.TryPlan(request, _spawnExecutor.SpawnPointList,
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
        public bool RequestSpawn(BattlefieldSpatialSnapshot snapshot, string squadOrder, int maxEnemyCount,
            int spawnBudget, EncounterExecutionContext context, out string result, out EncounterExecutionSummary summary)
        {
            summary = new EncounterExecutionSummary(0, 0, "Rejected");
            if (!ValidateExecution(snapshot, out result)) return false;

            if (string.IsNullOrWhiteSpace(squadOrder) || maxEnemyCount <= 0 || spawnBudget <= 0)
            {
                result = "분대 명령, 최대 생성 수와 스폰 비용 예산을 확인하세요.";
                return false;
            }

            var request = new SpawnRequest(squadOrder.Trim(), maxEnemyCount, spawnBudget);
            if (!_planner.TryPlan(request, _spawnExecutor.SpawnPointList, snapshot,
                    out IReadOnlyList<SpawnInstruction> instructions))
            {
                result = $"SpawnPlan 생성 실패: preset '{request.SquadOrder}', " +
                         $"maxCount {request.MaxCount}, spawnBudget {request.SpawnBudget}, " +
                         $"available points {_spawnExecutor.SpawnPointList.GetAvailableSpawnPoints().Count}. " +
                         "Grid 밖 또는 Player/Turret 점유 셀의 SpawnPoint는 제외됩니다.";
                summary = new EncounterExecutionSummary(0,0,"PlanRejected", new SpawnDecisionSnapshot(snapshot,
                    _planner.LastCandidates,System.Array.Empty<SpawnDecisionSnapshot.Plan>(),System.Array.Empty<SpawnDecisionSnapshot.Actual>(),result,
                    SpawnPointSpatialPolicy.Description));
                return false;
            }

            return ExecutePlan(snapshot, instructions, context, SpawnPointSpatialPolicy.Description,
                out result, out summary);
        }

        /// <summary>지점별 자동 편성 계획을 서버에서 실행한다. 외부의 고정 프리셋 요청과 구분되는 진입점이다.</summary>
        public bool RequestSpawn(BattlefieldSpatialSnapshot snapshot, AutomaticSpawnRequest request,
            SquadSelectionSettings settings, IRandomSource random, EncounterExecutionContext context,
            out string result, out EncounterExecutionSummary summary)
        {
            summary = new EncounterExecutionSummary(0, 0, "Rejected");
            if (!ValidateExecution(snapshot, out result)) return false;
            if (request.SpawnBudget <= 0 || request.MaxCount <= 0 || settings == null || !settings.IsValid ||
                random == null || !random.IsAvailable)
            {
                result = "자동 편성 예산·마릿수 상한·설정과 Core 권위 난수 주입을 확인하세요.";
                return false;
            }

            // CU도 Turret 후보에 포함된다. 요청당 한 번만 씬의 후보를 조회한다.
            TargetableComponent[] targets = Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None);
            var playerPositions = new List<BattlefieldPoint>();
            foreach (TargetableComponent candidate in targets)
            {
                if (candidate == null || candidate.TargetType != TargetType.Player ||
                    !candidate.CanBeTargeted || candidate.TargetTransform == null)
                    continue;

                Vector3 position = candidate.TargetTransform.position;
                playerPositions.Add(new BattlefieldPoint(position.x, position.y));
            }
            int totalEnemyCount = _spawnExecutor.CountForMatch(snapshot.MatchId);
            var candidates = new List<SpatialSpawnCandidate>();
            var targetPositions = new Dictionary<string, BattlefieldPoint>();
            foreach (SpawnPoint point in _spawnExecutor.SpawnPointList.GetAvailableSpawnPoints())
            {
                if (string.IsNullOrWhiteSpace(point.SpawnPointId) || targetPositions.ContainsKey(point.SpawnPointId))
                    continue;
                if (!EnemyTargetQuery.TryFindNearest(point.Position, targets, TargetType.Turret, out ITargetable target))
                    continue;

                Vector3 position = target.TargetTransform.position;
                var targetPosition = new BattlefieldPoint(position.x, position.y);
                if (!snapshot.Grid.TryGetCellAt(targetPosition, out _))
                    continue;

                candidates.Add(new SpatialSpawnCandidate(point.SpawnPointId,
                    new BattlefieldPoint(point.Position.x, point.Position.y)));
                targetPositions.Add(point.SpawnPointId, targetPosition);
            }
            if (!_planner.TryPlan(request, candidates, snapshot, targetPositions, totalEnemyCount,
                    playerPositions, settings, random, out var instructions))
            {
                result = $"자동 SpawnPlan 생성 실패: budget {request.SpawnBudget}, maxCount {request.MaxCount}. " +
                         "카탈로그, 지점·지역 예산, 셀 수용량과 편성 비용을 확인하세요.";
                summary = new EncounterExecutionSummary(0, 0, "PlanRejected",
                    new SpawnDecisionSnapshot(snapshot, _planner.LastCandidates,
                        System.Array.Empty<SpawnDecisionSnapshot.Plan>(), System.Array.Empty<SpawnDecisionSnapshot.Actual>(),
                        result, SpawnPointSpatialPolicy.AutomaticDescription));
                return false;
            }
            return ExecutePlan(snapshot, instructions, context, SpawnPointSpatialPolicy.AutomaticDescription,
                out result, out summary);
        }

        private bool ValidateExecution(BattlefieldSpatialSnapshot snapshot, out string result)
        {
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

            result = null;
            return true;
        }

        private bool ExecutePlan(BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpawnInstruction> instructions,
            EncounterExecutionContext context, string policy, out string result, out EncounterExecutionSummary summary)
        {
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
                int instructionSpawned = _spawnExecutor.Execute(instruction, context, actuals.Add,reason=>failure=reason);
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

            _planner.RecordExecuted(snapshot, actuals);
            result = $"Match {snapshot.MatchId} r{snapshot.Revision} → Encounter: " +
                     $"{snapshot.Triangles.Count} triangles, {snapshot.FrontlineEdges.Count} frontline edges, " +
                     $"{snapshot.Grid.OccupiedCells.Count} occupied cells; " +
                     $"{instructions.Count} plan(s), {spawnedCount} spawned. {executionSummary}";
            Debug.Log(result);
            summary = new EncounterExecutionSummary(plannedCount, spawnedCount,
                spawnedCount == plannedCount ? "Spawned" : spawnedCount > 0 ? "PartialSpawn" : "NoSpawn",
                new SpawnDecisionSnapshot(snapshot,_planner.LastCandidates,plans,actuals,
                    spawnedCount == plannedCount ? "Spawned" : spawnedCount > 0 ? "PartialSpawn" : "NoSpawn",
                    policy));
            return spawnedCount > 0;
        }
    }
}


// Match 공간 자료로 편성 계획을 요청하고 실제 생성 결과를 진단 자료로 반환합니다.
// 정상 요청은 호출자·Game Debugger가 관찰하며 실패·선택 진단은 기존 정책을 유지합니다.
using System;
using System.Collections.Generic;
using System.Text;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Domain
{
    /// <summary>
    /// Match 공간 정보를 Planner에 전달하고 분대 생성 결과를 보고한다.
    /// 스폰 계획은 Planner에, 실제 네트워크 생성은 Executor에 위임한다.
    /// </summary>
    public class EncounterRuntime
    {
        private readonly SpawnCompositionPlanner _planner;
        internal SpawnDistributionHistory.State CaptureCheckpoint() => _planner.CaptureCheckpoint();
        internal void RestoreCheckpoint(SpawnDistributionHistory.State state, MatchId match) => _planner.RestoreCheckpoint(state, match);

        private readonly EnemySpawnExecutor _spawnExecutor;
        private readonly MatchId _matchId;
        private readonly EncounterBudgetSettings _budgetSettings;
        private bool _budgetInitialized;
        private int _currentBudget;

        private static readonly Unity.Profiling.ProfilerMarker PlanMarker = new Unity.Profiling.ProfilerMarker("TeamHJD.Encounter.Plan");
        private static readonly Unity.Profiling.ProfilerMarker SpawnMarker = new Unity.Profiling.ProfilerMarker("TeamHJD.Encounter.SpawnAndCommit");

        /// <summary>
        /// 분대 프리셋과 몬스터 카탈로그가 준비되어 계획을 세울 수 있는지 반환한다.
        /// </summary>
        public bool HasCatalog => _planner.HasCatalog;

        /// <summary>
        /// 전달받은 Executor가 유효한지 반환한다.
        /// </summary>
        public bool HasExecutor => _spawnExecutor != null;
        public bool CanExecute => _spawnExecutor != null && _spawnExecutor.CanSpawn;
        public EnemySpawnExecutor Executor => _spawnExecutor;
        
        /// <summary>
        /// Match별 실행기와 설정으로 Planner를 준비한다. 생성과 수명은 호출자가 관리한다.
        /// </summary>
        public EncounterRuntime(MatchId matchId, EnemySpawnExecutor executor,
            EnemySquadPresetCatalog catalog, EncounterBudgetSettings budgetSettings)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Match ID가 필요합니다.", nameof(matchId));
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            _matchId = matchId;
            _spawnExecutor = executor;
            _budgetSettings = budgetSettings ?? throw new ArgumentNullException(nameof(budgetSettings));
            _planner = new SpawnCompositionPlanner(catalog, executor.Catalog);
        }

        /// <summary>최신 Core 자료로 예산을 계산하고 상황별 편성을 계획해 생성한다.</summary>
        public bool RequestSpawn(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region,
            int maximumEnemyCount, SquadSelectionSettings settings, IRandomSource random,
            EncounterExecutionContext context, out string result, out EncounterExecutionSummary summary)
        {
            summary = new EncounterExecutionSummary(0, 0, "Rejected");
            if (!ValidateExecution(battlefield, out result)) return false;
            if (maximumEnemyCount <= 0 || settings == null || !settings.IsValid ||
                random == null || !random.IsAvailable)
            {
                result = "생성 상한·편성 설정·Core 난수를 확인하세요.";
                return false;
            }
            if (context != null && context.SpawnCancellation.IsCancellationRequested)
            {
                result = "The Match/source no longer accepts pending spawns.";
                summary = new EncounterExecutionSummary(0, 0, "OriginUnavailable");
                return false;
            }
            if (context?.Placement != null && context.Placement.Origin.Delivery != SpawnDeliveryMode.Immediate)
            {
                result = "Ground emergence / air drop transitions require an explicit Enemy delivery adapter.";
                summary = new EncounterExecutionSummary(0, 0, "UnsupportedDelivery");
                return false;
            }
            if (!TryGetSpawnBudget(battlefield, region, context, settings.logSelectionDecisions,
                    out int budget, out result)) return false;

            return PlanAreaSpawn(battlefield, new AreaSpawnPlanRequest(budget, maximumEnemyCount),
                settings, random, context, out result, out summary);
        }

        /// <summary>같은 Match 예산으로 지정된 프리셋을 계획해 생성한다.</summary>
        public bool RequestSpawn(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region,
            string squadOrder, int maximumEnemyCount, EncounterExecutionContext context,
            out string result, out EncounterExecutionSummary summary, bool logBudgetDecisions = false,
            EncounterEnvironmentProfile environmentProfile = null)
        {
            summary = new EncounterExecutionSummary(0, 0, "Rejected");
            if (!ValidateExecution(battlefield, out result)) return false;
            if (string.IsNullOrWhiteSpace(squadOrder) || maximumEnemyCount <= 0)
            {
                result = "분대 명령과 생성 상한을 확인하세요.";
                return false;
            }
            if (context != null && context.SpawnCancellation.IsCancellationRequested)
            {
                result = "The Match/source no longer accepts pending spawns.";
                summary = new EncounterExecutionSummary(0, 0, "OriginUnavailable");
                return false;
            }
            if (context?.Placement != null && context.Placement.Origin.Delivery != SpawnDeliveryMode.Immediate)
            {
                result = "Ground emergence / air drop transitions require an explicit Enemy delivery adapter.";
                summary = new EncounterExecutionSummary(0, 0, "UnsupportedDelivery");
                return false;
            }
            if (!TryGetSpawnBudget(battlefield, region, context, logBudgetDecisions,
                    out int budget, out result)) return false;

            return PlanFixedSpawn(battlefield, squadOrder, maximumEnemyCount, budget, context,
                out result, out summary, environmentProfile);
        }

        /// <summary>
        /// Match 공간 snapshot을 Encounter 경계로 받아 요청한 PoC 분대를 계획하고 서버 Executor에 전달합니다.
        /// Grid의 점유·적 밀도와 Frontline 거리를 PoC 배치 정책에 적용합니다.
        /// </summary>
        private bool PlanFixedSpawn(BattlefieldSpatialSnapshot snapshot, string squadOrder, int maxEnemyCount,
            int spawnBudget, EncounterExecutionContext context, out string result, out EncounterExecutionSummary summary,
            EncounterEnvironmentProfile environmentProfile = null)
        {
            var request = new FixedPresetSpawnRequest(squadOrder.Trim(), maxEnemyCount, spawnBudget);
            IReadOnlyList<SpawnInstruction> instructions;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            bool planned;
            using (PlanMarker.Auto())
                planned = context?.Placement == null
                    ? _planner.TryPlan(request, _spawnExecutor.SpawnPointList, snapshot, context?.Environment, environmentProfile, out instructions)
                    : _planner.TryPlanOrigin(request, _spawnExecutor.SpawnPointList, snapshot, context.Environment, environmentProfile, context.Placement, out instructions);
            clock.Stop();
            if (!planned)
            {
                result = $"SpawnPlan 생성 실패: preset '{request.SquadOrder}', maxCount {request.MaxCount}, spawnBudget {request.SpawnBudget}.";
                summary = new EncounterExecutionSummary(0, 0, "PlanRejected", new SpawnDecisionSnapshot(snapshot,
                    _planner.LastCandidates, Array.Empty<SpawnDecisionSnapshot.Plan>(), Array.Empty<SpawnDecisionSnapshot.Actual>(), result,
                    SpawnPointSpatialPolicy.Description, context?.Environment, _planner.LastEnvironmentPolicy, context?.Placement, clock.Elapsed.TotalMilliseconds));
                return false;
            }
            return ExecutePlan(snapshot, instructions, context, SpawnPointSpatialPolicy.Description,
                out result, out summary, clock.Elapsed.TotalMilliseconds);
        }

        /// <summary>확정된 요청 예산 안에서 지점별 편성을 계획하고 실행한다.</summary>
        private bool PlanAreaSpawn(BattlefieldSpatialSnapshot snapshot, AreaSpawnPlanRequest request,
            SquadSelectionSettings settings, IRandomSource random, EncounterExecutionContext context,
            out string result, out EncounterExecutionSummary summary)
        {
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
            var targetAttackTypes = new Dictionary<string, TargetAttackType>();
            var targetNames = settings.logSelectionDecisions ? new Dictionary<string, string>() : null;
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
                targetNames?.Add(point.SpawnPointId, target.TargetTransform.name);
                DamageableTargetAdapter adapter = target.TargetTransform.GetComponent<DamageableTargetAdapter>();
                targetAttackTypes.Add(point.SpawnPointId,
                    adapter != null ? adapter.AttackType : TargetAttackType.Unknown);
            }
            bool planned = _planner.TryPlan(request, candidates, snapshot, targetPositions, targetAttackTypes, totalEnemyCount,
                    playerPositions, settings, random, out var instructions,
                    settings.logSelectionDecisions
                        ? (pointId, area, presets, selected) => LogSquadSelection(snapshot, pointId,
                            targetNames[pointId], targetPositions[pointId], area, presets, selected, settings)
                        : null);
            if (settings.logSelectionDecisions)
            {
                var budgets = new StringBuilder();
                long allocated = 0;
                foreach (SpawnBudgetAllocation allocation in _planner.LastBudgetAllocations)
                {
                    if (budgets.Length > 0) budgets.Append(" | ");
                    budgets.Append(allocation.Id).Append(": ").Append(allocation.Budget)
                        .Append(" (공유 지점 ").Append(allocation.SpawnPointIds.Count).Append(')');
                    allocated += allocation.Budget;
                }
                Debug.Log($"[SpawnBudget] Match {snapshot.MatchId} r{snapshot.Revision}: " +
                    $"전체 예산 {request.SpawnBudget}, 지역 예산 합계 {allocated}. {budgets}");
            }
            if (!planned)
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

        /// <summary>
        /// 동일한 Match의 자료와 예산 설정을 확인한 뒤 점유율 목표를 이번 요청 예산으로 변환한다.
        /// </summary>
        private bool TryGetSpawnBudget(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region,
            EncounterExecutionContext context, bool logDecisions, out int budget, out string reason)
        {
            budget = 0;
            if (battlefield == null || battlefield.MatchId != _matchId ||
                region == null || region.MatchId != _matchId ||
                context == null || context.MatchId != _matchId)
            {
                reason = "동일한 Match의 전장·지역 정보와 요청이 필요합니다.";
                return false;
            }
            if (_budgetSettings.baseBudget < 1 ||
                _budgetSettings.maximumBudget < _budgetSettings.baseBudget ||
                (region.Mode == RegionPolicyMode.Static && _budgetSettings.staticIncreasePerRequest < 1))
            {
                reason = "Encounter 예산 설정을 확인하세요.";
                return false;
            }

            int targetBudget = SpawnBudgetPolicy.CalculateTotalBudget(
                region, _budgetSettings.baseBudget, _budgetSettings.maximumBudget);
            budget = UpdateSpawnBudget(targetBudget, region.Mode);
            if (logDecisions)
            {
                double ratio = (double)region.CapturedCellCount / region.Definition.Geometry.TotalCellCount;
                Debug.Log($"[EncounterBudget] Match {_matchId} region r{region.Revision}, mode={region.Mode}: " +
                    $"점유 셀 {region.CapturedCellCount}/{region.Definition.Geometry.TotalCellCount} ({ratio:P1}), " +
                    $"목표 예산 {targetBudget}, 적용 예산 {budget}");
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// 최초·Dynamic 요청은 목표를 즉시 적용하고 Static 증가는 요청당 설정된 폭으로 제한한다.
        /// </summary>
        private int UpdateSpawnBudget(int targetBudget, RegionPolicyMode mode)
        {
            if (!_budgetInitialized || mode == RegionPolicyMode.Dynamic)
            {
                _budgetInitialized = true;
                _currentBudget = targetBudget;
                return _currentBudget;
            }

            _currentBudget = (int)Math.Min(
                (long)_currentBudget + _budgetSettings.staticIncreasePerRequest,
                targetBudget);
            return _currentBudget;
        }

        /// <summary>
        /// 기준 타깃과 집계 상황, 후보 가중치와 선택 결과를 진단 로그로 출력한다.
        /// </summary>
        private static void LogSquadSelection(BattlefieldSpatialSnapshot snapshot, string pointId,
            string targetName, BattlefieldPoint targetPosition, SpawnAreaContext area,
            IReadOnlyList<EnemySquadPreset> presets, EnemySquadPreset selected, SquadSelectionSettings settings)
        {
            int capacity = settings.maxEnemiesPerCell - area.CellEnemyCount;
            double totalWeight = 0d;
            foreach (EnemySquadPreset preset in presets)
                if (preset.TotalWeight > 0 && preset.TotalWeight <= capacity)
                    totalWeight += SquadSelectionPolicy.CalculateWeight(preset, area, settings);

            var weights = new StringBuilder();
            foreach (EnemySquadPreset preset in presets)
            {
                if (weights.Length > 0) weights.Append(" | ");
                weights.Append(preset.SquadOrder);
                if (preset.TotalWeight <= 0 || preset.TotalWeight > capacity)
                {
                    weights.Append(": 제외(셀 수용량)");
                    continue;
                }

                double weight = SquadSelectionPolicy.CalculateWeight(preset, area, settings);
                weights.Append($": {preset.SelectionWeight} → {weight:0.##} ({weight / totalWeight:P1})");
            }

            Debug.Log($"[SquadSelection] Match {snapshot.MatchId} r{snapshot.Revision} @ {pointId}\n" +
                $"기준 타깃: {targetName} ({targetPosition.X:0.##}, {targetPosition.Y:0.##}), 공격 유형: {area.AttackType}\n" +
                $"주변 병력: {area.NearbyEnemyCount}/{area.TotalEnemyCount} ({area.NearbyEnemyPercentage:0.##}%), " +
                $"판정: {(area.NearbyEnemyPercentage <= settings.sparseEnemyPercentage ? "적음" : "많음")} " +
                $"(기준 ≤ {settings.sparseEnemyPercentage:0.##}%), 주변 플레이어: {area.HasNearbyPlayer}, " +
                $"출현 셀 잔여 수용량: {capacity}\n" +
                $"후보 가중치(기본 → 최종 / 선택 확률): {weights}\n" +
                $"예산·마릿수 제한을 통과한 후보만 표시. 선택: {selected.SquadOrder}");
        }

        /// <summary>
        /// 요청의 Match와 서버 실행기 구성이 현재 Runtime에서 실행 가능한지 확인한다.
        /// </summary>
        private bool ValidateExecution(BattlefieldSpatialSnapshot snapshot, out string result)
        {
            if (snapshot == null || snapshot.MatchId != _matchId)
            {
                result = "현재 Runtime의 Match에 해당하는 Battlefield snapshot이 필요합니다.";
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

        /// <summary>
        /// 계획된 명령을 실행해 실제 생성 수를 집계하고 생성 이력과 진단 결과를 반환한다.
        /// </summary>
        private bool ExecutePlan(BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpawnInstruction> instructions,
            EncounterExecutionContext context, string policy, out string result, out EncounterExecutionSummary summary, double planMilliseconds = 0d)
        {
            var executionSummary = new StringBuilder();
            int spawnedCount = 0;
            int plannedCount = 0;
            var plans = new List<SpawnDecisionSnapshot.Plan>();
            var actuals = new List<SpawnDecisionSnapshot.Actual>();
            var spawnClock = System.Diagnostics.Stopwatch.StartNew();
            using (SpawnMarker.Auto())
            {
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
            }
            spawnClock.Stop();
            result = $"Match {snapshot.MatchId} r{snapshot.Revision} → Encounter: " +
                     $"{snapshot.Triangles.Count} triangles, {snapshot.FrontlineEdges.Count} frontline edges, " +
                     $"{snapshot.Grid.OccupiedCells.Count} occupied cells; " +
                     $"{instructions.Count} plan(s), {spawnedCount} spawned. {executionSummary} " +
                     $"Environment r{context?.Environment?.Revision ?? 0}, policy={_planner.LastEnvironmentPolicy}";
            summary = new EncounterExecutionSummary(plannedCount, spawnedCount,
                spawnedCount == plannedCount ? "Spawned" : spawnedCount > 0 ? "PartialSpawn" : "NoSpawn",
                new SpawnDecisionSnapshot(snapshot,_planner.LastCandidates,plans,actuals,
                    spawnedCount == plannedCount ? "Spawned" : spawnedCount > 0 ? "PartialSpawn" : "NoSpawn",
                    policy, context?.Environment, _planner.LastEnvironmentPolicy, context?.Placement,
                    planMilliseconds, spawnClock.Elapsed.TotalMilliseconds));
            return spawnedCount > 0;
        }
    }
}


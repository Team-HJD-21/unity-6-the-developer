// Planner의 예산·지역 배분·편성·최종 명령 구성을 순서대로 조율합니다.
// 기존 비용/분산/선택 Policy를 재사용하며 Scene 수집과 실제 생성은 하지 않습니다.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using Debug = UnityEngine.Debug;
namespace TeamHJD.Game.Enemies.Encounter
{
    public sealed class EncounterPlanner
    {
        private readonly MatchId _matchId;
        private readonly EncounterBudgetSettings _budgetSettings;
        private readonly SpawnCompositionPlanner _composition;
        private bool _budgetInitialized;
        private int _currentBudget;
        public bool HasCatalog => _composition.HasCatalog;
        internal int CapturedBudget => _budgetInitialized ? _currentBudget : 0;
        internal void RestoreBudget(int budget)
        {
            if (budget < 0) throw new ArgumentOutOfRangeException(nameof(budget));
            _budgetInitialized = budget > 0; _currentBudget = budget;
        }
        internal SpawnDistributionHistory.State CaptureCheckpoint() => _composition.CaptureCheckpoint();
        internal void RestoreCheckpoint(SpawnDistributionHistory.State state, MatchId match) => _composition.RestoreCheckpoint(state, match);
        internal void RecordExecuted(BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpawnDecisionSnapshot.Actual> actuals)
            => _composition.RecordExecuted(snapshot, actuals);

        public EncounterPlanner(MatchId matchId, EnemySquadPresetCatalog squads, EnemyCatalog enemies, EncounterBudgetSettings settings)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A Match ID is required.", nameof(matchId));
            _matchId = matchId;
            _budgetSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            _composition = new SpawnCompositionPlanner(squads, enemies);
        }

        /// <summary>Pipeline 전용 계획 Step입니다. 예산 결정 뒤 기존 분배·선택 정책이 명령을 구성합니다.</summary>
        internal bool TryPlan(EncounterSituation situation, string fixedPreset, int maximumCount,
            SquadSelectionSettings settings, IRandomSource random, EncounterExecutionContext context,
            EncounterEnvironmentProfile environmentProfile, out EncounterPlan plan, out string reason)
        {
            var collection = situation.Collection;
            var snapshot = collection.Battlefield;
            bool automatic = fixedPreset == null;
            string policy = automatic ? SpawnPointSpatialPolicy.AutomaticDescription : SpawnPointSpatialPolicy.Description;
            plan = null;
            if (!TryDetermineBudget(snapshot, collection.Region, context, settings?.logSelectionDecisions == true,
                    out int budget, out reason)) return false;
            var timer = Stopwatch.StartNew();
            IReadOnlyList<SpawnInstruction> instructions;
            bool planned;
            if (automatic)
            {
                var request = new AreaSpawnPlanRequest(budget, maximumCount);
                planned = _composition.TryPlan(request, situation.Candidates, snapshot,
                    situation.TargetPositions, situation.TargetAttackTypes, collection.EnemyCount,
                    situation.PlayerPositions, settings, random, out instructions,
                    settings.logSelectionDecisions
                        ? (point, area, presets, selected) => LogSquadSelection(snapshot, point,
                            situation.TargetNames[point], situation.TargetPositions[point], area, presets, selected, settings)
                        : null, context.Environment, environmentProfile);
            }
            else
            {
                var request = new FixedPresetSpawnRequest(fixedPreset.Trim(), maximumCount, budget);
                planned = context.Placement == null
                    ? _composition.TryPlan(request, collection.Points, snapshot, context.Environment, environmentProfile, out instructions)
                    : _composition.TryPlanOrigin(request, collection.Points, snapshot, context.Environment,
                        environmentProfile, context.Placement, out instructions);
            }
            timer.Stop();
            var observations = situation.ExcludedPoints.Concat(_composition.LastCandidates)
                .GroupBy(candidate => candidate.Id, StringComparer.Ordinal).Select(group => group.First()).ToArray();
            plan = new EncounterPlan(snapshot, instructions, observations, policy,
                _composition.LastEnvironmentPolicy, timer.Elapsed.TotalMilliseconds);
            reason = planned ? null : "No spawn plan satisfies eligible regions, spatial limits, composition and budget.";
            return planned;
        }

        public bool TryDetermineBudget(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region,
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
    }
}

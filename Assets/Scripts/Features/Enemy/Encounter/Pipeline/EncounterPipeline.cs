// Collector → Processor → Planner → Executor의 호출 순서만 조율합니다.
// Match/Host 검증 전에는 조회·예산·난수를 사용하지 않으며 실제 결과만 이력에 반영합니다.
// 게임·디버그 요청은 공통 요청 서비스를 거쳐 이 진입점에 도착합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Enemies.Encounter
{
    public sealed class EncounterPipeline
    {
        private readonly MatchId _matchId;
        private readonly EnemySpawnExecutor _executor;
        private readonly EncounterCollector _collector;
        private readonly EncounterProcessor _processor = new EncounterProcessor();
        private readonly EncounterPlanner _planner;
        private readonly EncounterExecutorStep _execution;
        private static readonly Unity.Profiling.ProfilerMarker CollectMarker = new Unity.Profiling.ProfilerMarker("TeamHJD.Encounter.Collect");
        private static readonly Unity.Profiling.ProfilerMarker PlanMarker = new Unity.Profiling.ProfilerMarker("TeamHJD.Encounter.Plan");
        private double _capturedSpawnMargin;
        private readonly IEncounterDeferredSpawnSink _deferred;
        private readonly EncounterBudgetSettings _settings;

        public bool HasCatalog => _planner.HasCatalog;
        public bool HasExecutor => _executor != null;
        public bool CanExecute => HasExecutor && _executor.CanSpawn;
        public EnemySpawnExecutor Executor => _executor;
        /// <summary>점유 셀에서 일반 스폰을 떨어뜨리는 추가 거리입니다. 월드 XY 단위입니다.</summary>
        public double CapturedSpawnMargin
        {
            get => _capturedSpawnMargin;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _capturedSpawnMargin = value;
            }
        }
        internal int CapturedBudget => _planner.CapturedBudget;
        internal double AdditionalCooldownRemaining(double now) => _planner.AdditionalCooldownRemaining(now);
        internal void RestoreAdditionalCooldown(double now, double remaining, Guid epoch, Guid grid)
            => _planner.RestoreAdditionalCooldown(now, remaining, epoch, grid);
        internal void RestoreBudget(int budget) => _planner.RestoreBudget(budget);
        internal SpawnDistributionHistory.State CaptureCheckpoint() => _planner.CaptureCheckpoint();
        internal void RestoreCheckpoint(SpawnDistributionHistory.State state, MatchId match) => _planner.RestoreCheckpoint(state, match);

        public EncounterPipeline(MatchId matchId, EnemySpawnExecutor executor, EnemySquadPresetCatalog squads,
            EncounterBudgetSettings settings, Func<RegionGridSnapshot> currentRegion = null)
            : this(matchId, executor, squads, settings, currentRegion, null) { }

        internal EncounterPipeline(MatchId matchId, EnemySpawnExecutor executor, EnemySquadPresetCatalog squads,
            EncounterBudgetSettings settings, Func<RegionGridSnapshot> currentRegion, IEncounterDeferredSpawnSink deferred)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A Match ID is required.", nameof(matchId));
            _matchId = matchId;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _deferred = deferred;
            _executor = executor != null ? executor : throw new ArgumentNullException(nameof(executor));
            _collector = new EncounterCollector(executor);
            _planner = new EncounterPlanner(matchId, squads, executor.Catalog, settings);
            _execution = new EncounterExecutorStep(executor, currentRegion);
        }

        /// <summary>Core 요청 서비스가 승인한 자동 편성 요청을 현재 전장·공용 난수로 처리합니다.</summary>
        public bool SpawnRequest(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region,
            int maximumEnemyCount, SquadSelectionSettings settings, IRandomSource random,
            EncounterExecutionContext context, out string result, out EncounterExecutionSummary summary,
            EncounterEnvironmentProfile environmentProfile = null)
            => Run(battlefield, region, null, maximumEnemyCount, settings, random, context, environmentProfile, out result, out summary);

        /// <summary>지정 편성의 게임 사건·디버그 요청을 같은 네 Step으로 처리합니다.</summary>
        public bool SpawnRequest(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region,
            string squadOrder, int maximumEnemyCount, EncounterExecutionContext context,
            out string result, out EncounterExecutionSummary summary, bool logBudgetDecisions = false,
            EncounterEnvironmentProfile environmentProfile = null)
            => Run(battlefield, region, squadOrder, maximumEnemyCount,
                new SquadSelectionSettings { logSelectionDecisions = logBudgetDecisions }, null,
                context, environmentProfile, out result, out summary);

        internal bool ExecutePrepared(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region,
            EncounterDeferredSpawn deferred, EncounterExecutionContext context, out string result, out EncounterExecutionSummary summary)
        {
            summary = new EncounterExecutionSummary(0, 0, "Rejected");
            result = "Prepared Encounter Match/authority is unavailable.";
            if (!CanExecute || battlefield == null || battlefield.MatchId != _matchId || region == null ||
                region.MatchId != _matchId || deferred == null || context.SpawnCancellation.IsCancellationRequested) return false;
            if (_planner.Cost(deferred.Instruction) != deferred.Cost)
            {
                result = "Prepared composition cost changed after its warning.";
                summary = new EncounterExecutionSummary(0, 0, "PlanRejected");
                return false;
            }
            var collected = _collector.Collect(battlefield, region, CapturedSpawnMargin);
            var plan = new EncounterPlan(battlefield, new[] { deferred.Instruction }, Array.Empty<SpawnDecisionSnapshot.Candidate>(),
                "공통 예산 내부 출현 / " + deferred.Reason, "prepared", 0);
            IReadOnlyList<SpawnDecisionSnapshot.Actual> actuals = Array.Empty<SpawnDecisionSnapshot.Actual>();
            try { return _execution.Execute(collected, plan, context, CapturedSpawnMargin, out result, out summary, out actuals); }
            finally { _planner.RecordExecuted(battlefield, actuals); }
        }

        private bool Run(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region, string preset, int maximum,
            SquadSelectionSettings settings, IRandomSource random, EncounterExecutionContext context,
            EncounterEnvironmentProfile environmentProfile, out string result, out EncounterExecutionSummary summary)
        {
            summary = new EncounterExecutionSummary(0, 0, "Rejected");
            result = "현재 Host Match의 전장·지역·요청이 필요합니다.";
            if (battlefield == null || battlefield.MatchId != _matchId || region == null || region.MatchId != _matchId ||
                context == null || context.MatchId != _matchId || !CanExecute) return false;
            if (maximum <= 0 || settings == null || !settings.IsValid ||
                (preset == null && (random == null || !random.IsAvailable)) ||
                (preset != null && string.IsNullOrWhiteSpace(preset)))
            { result = "편성·생성 상한·Core 난수 설정을 확인하세요."; return false; }
            if (context.SpawnCancellation.IsCancellationRequested)
            {
                result = "The Match/source no longer accepts pending spawns.";
                summary = new EncounterExecutionSummary(0, 0, "OriginUnavailable");
                return false;
            }
            if (preset == null && context.Placement != null)
            {
                result = "Automatic composition currently requires automatic origins; explicit areas require a fixed or prepared composition.";
                summary = new EncounterExecutionSummary(0, 0, "UnsupportedOrigin");
                return false;
            }
            if (context.Placement != null && context.Placement.Origin.Delivery != SpawnDeliveryMode.Immediate)
            {
                result = "The requested delivery requires an Enemy delivery adapter.";
                summary = new EncounterExecutionSummary(0, 0, "UnsupportedDelivery");
                return false;
            }

            bool canDefer = preset == null && _deferred?.CanShowWarning == true;
            bool collectInternal = canDefer && _planner.ShouldCollectAdditional(region, context.CaptureTrend);
            EncounterCollection collected;
            using (CollectMarker.Auto())
                collected = _collector.Collect(battlefield, region, CapturedSpawnMargin,
                    collectInternal, _settings.additionalSpawns?.minimumPlayerDistance ?? 8);
            EncounterSituation situation = _processor.Process(collected);
            EncounterPlan plan;
            bool planned;
            using (PlanMarker.Auto())
                planned = _planner.TryPlan(situation, preset, maximum, settings, random, context,
                    environmentProfile, out plan, out result, canDefer);
            if (!planned)
            {
                summary = new EncounterExecutionSummary(0, 0, "PlanRejected", plan == null ? null :
                    new SpawnDecisionSnapshot(battlefield, plan.Candidates, Array.Empty<SpawnDecisionSnapshot.Plan>(),
                        Array.Empty<SpawnDecisionSnapshot.Actual>(), result, plan.Policy, context.Environment,
                        plan.EnvironmentPolicy, context.Placement, plan.PlanningMilliseconds));
                return false;
            }

            IReadOnlyList<SpawnDecisionSnapshot.Actual> actuals = Array.Empty<SpawnDecisionSnapshot.Actual>();
            try
            {
                bool spawned = _execution.Execute(collected, plan, context, CapturedSpawnMargin, out result, out summary, out actuals);
                if (plan.Deferred != null)
                {
                    bool queued = spawned && _deferred.TrySchedulePrepared(plan.Deferred, context);
                    if (queued)
                    {
                        _planner.RecordAdditionalQueued(context, region);
                        result += " / 추가 출현 예고: " + plan.Deferred.Reason;
                    }
                    else result += " / 내부 추가 출현은 예약되지 않았습니다. 자동 재생성하지 않습니다.";
                }
                return spawned;
            }
            finally { _planner.RecordExecuted(battlefield, actuals); }
        }
    }
}

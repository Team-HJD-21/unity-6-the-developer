// Squad Preset과 요청 상한을 지점별 SpawnInstruction으로 변환합니다.
// Match snapshot이 있으면 공간 정책으로 후보와 분량을 선택합니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;

/// <summary>
/// 분대 프리셋의 비율과 스폰 지점, 선택적으로 Grid·전선을 이용해 생성 명령을 만든다.
/// 생성 자체는 담당하지 않으며, 점령·아군 점유 지점에는 생성 수를 배정하지 않는다.
/// </summary>
public sealed class SpawnCompositionPlanner
{
    private readonly EnemySquadPresetCatalog _catalog;
    private readonly EnemyCatalog _enemyCatalog;
    private readonly SpawnPointSpatialPolicy _spatialPolicy = new SpawnPointSpatialPolicy();
    internal SpawnDistributionHistory.State CaptureCheckpoint() => _spatialPolicy.CaptureCheckpoint();
    internal void RestoreCheckpoint(SpawnDistributionHistory.State state, MatchId match) => _spatialPolicy.RestoreCheckpoint(state, match);

    public IReadOnlyList<SpawnDecisionSnapshot.Candidate> LastCandidates { get; private set; } = Array.Empty<SpawnDecisionSnapshot.Candidate>();
    public IReadOnlyList<SpawnBudgetAllocation> LastBudgetAllocations { get; private set; } = Array.Empty<SpawnBudgetAllocation>();

    public MatchEnvironmentSnapshot LastEnvironment { get; private set; }
    public string LastEnvironmentPolicy { get; private set; } = "neutral";

    /// <summary>
    /// 분대 프리셋과 몬스터 비용을 조회할 카탈로그가 준비되었는지 반환한다.
    /// </summary>
    public bool HasCatalog => _catalog != null && _enemyCatalog != null;

    /// <summary>Executor가 실제로 생성한 적만 Match별 공간 배분 이력에 반영합니다.</summary>
    public void RecordExecuted(BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpawnDecisionSnapshot.Actual> actuals)
        => _spatialPolicy.RecordExecuted(snapshot, actuals);

    /// <summary>
    /// 분대 구성과 몬스터 생성 비용을 조회할 카탈로그를 전달받는다.
    /// </summary>
    /// <param name="catalog">분대 명령으로 조회할 프리셋 카탈로그.</param>
    /// <param name="enemyCatalog">몬스터 정의와 생성 비용을 조회할 카탈로그.</param>
    public SpawnCompositionPlanner(EnemySquadPresetCatalog catalog, EnemyCatalog enemyCatalog)
    {
        _catalog = catalog;
        _enemyCatalog = enemyCatalog;
    }

    /// <summary>비용 예산과 마릿수 상한 안에서 생성 가능한 편성을 반환한다.</summary>
    public IReadOnlyList<EnemySquadPreset> GetSpawnablePresets(int spawnBudget, int maxCount)
    {
        var candidates = new List<EnemySquadPreset>();

        if (!HasCatalog || spawnBudget <= 0 || maxCount <= 0)
            return candidates;

        foreach (EnemySquadPreset preset in _catalog.Presets)
        {
            if (preset == null)
                continue;

            int count = preset.TotalWeight;
            int cost = preset.CalculateCost(_enemyCatalog);

            if (count > 0 && count <= maxCount &&
                cost > 0 && cost <= spawnBudget)
            {
                candidates.Add(preset);
            }
        }

        return candidates;
    }

    /// <summary>지점별 상황으로 한 편성씩 선택하며 요청 전체와 선택적 예산 묶음의 잔액을 함께 차감한다.</summary>
    public bool TryPlan(AreaSpawnPlanRequest request,
        IReadOnlyList<SpatialSpawnCandidate> spawnPoints, BattlefieldSpatialSnapshot snapshot,
        IReadOnlyDictionary<string, BattlefieldPoint> targetPositions,
        IReadOnlyDictionary<string, TargetAttackType> targetAttackTypes,
        int totalEnemyCount,
        IReadOnlyList<BattlefieldPoint> playerPositions,
        SquadSelectionSettings settings, IRandomSource random,
        out IReadOnlyList<SpawnInstruction> instructions,
        Action<string, SpawnAreaContext, IReadOnlyList<EnemySquadPreset>, EnemySquadPreset> onSelected = null,
        MatchEnvironmentSnapshot environment = null, EncounterEnvironmentProfile environmentProfile = null)
    {
        instructions = Array.Empty<SpawnInstruction>();
        LastCandidates = Array.Empty<SpawnDecisionSnapshot.Candidate>();
        LastBudgetAllocations = Array.Empty<SpawnBudgetAllocation>();
        LastEnvironment = environment;
        LastEnvironmentPolicy = "neutral";
        string requiredPreset = null;
        if (environment != null && (snapshot == null || environment.MatchId != snapshot.MatchId)) return false;
        if (environmentProfile != null)
        {
            request = environmentProfile.Select(request, environment, out requiredPreset, out string policy);
            LastEnvironmentPolicy = policy;
        }
        if (!HasCatalog || request.SpawnBudget <= 0 || request.MaxCount <= 0 ||
            snapshot?.Grid == null || spawnPoints == null || targetPositions == null || targetAttackTypes == null ||
            totalEnemyCount < 0 ||
            playerPositions == null || settings == null || !settings.IsValid ||
            random == null || !random.IsAvailable)
            return false;

        // 비용은 요청마다 한 번만 읽고, 카탈로그 순서와 무관하게 동일한 난수 흐름을 사용한다.
        var presets = new List<EnemySquadPreset>();
        var costs = new Dictionary<EnemySquadPreset, int>();
        var orders = new HashSet<string>(StringComparer.Ordinal);
        foreach (EnemySquadPreset preset in GetSpawnablePresets(request.SpawnBudget, request.MaxCount))
        {
            if (!string.IsNullOrWhiteSpace(requiredPreset) && preset.SquadOrder != requiredPreset) continue;
            if (string.IsNullOrWhiteSpace(preset.SquadOrder) || preset.SelectionWeight <= 0 ||
                !Enum.IsDefined(typeof(EnemySquadRole), preset.Role) || !orders.Add(preset.SquadOrder))
                continue;
            presets.Add(preset);
            costs.Add(preset, preset.CalculateCost(_enemyCatalog));
        }
        if (presets.Count == 0) return false;
        presets.Sort((left, right) => StringComparer.Ordinal.Compare(left.SquadOrder, right.SquadOrder));

        var session = _spatialPolicy.BeginPlanning(snapshot, spawnPoints, settings);
        int MinimumCostForCapacity(int capacity)
        {
            int minimum = int.MaxValue;
            foreach (EnemySquadPreset preset in presets)
                if (preset.TotalWeight <= capacity)
                    minimum = Math.Min(minimum, costs[preset]);
            return minimum == int.MaxValue ? 0 : minimum;
        }
        IReadOnlyList<SpawnBudgetAllocation> allocations = request.BudgetAllocations
            ?? SpawnBudgetPolicy.Allocate(request.SpawnBudget,
                session.GetBudgetAreas(targetPositions, totalEnemyCount, playerPositions, MinimumCostForCapacity),
                settings);
        LastBudgetAllocations = allocations;

        var allocationsByPoint = new Dictionary<string, SpawnBudgetAllocation>(StringComparer.Ordinal);
        var allocationRemaining = new Dictionary<SpawnBudgetAllocation, int>();
        foreach (SpawnBudgetAllocation allocation in allocations)
        {
            allocationRemaining.Add(allocation, allocation.Budget);
            foreach (string id in allocation.SpawnPointIds) allocationsByPoint.Add(id, allocation);
        }

        int remainingBudget = request.SpawnBudget;
        int remainingCount = request.MaxCount;
        int BudgetFor(SpatialSpawnCandidate point)
        {
            return allocationsByPoint.TryGetValue(point.Id, out SpawnBudgetAllocation allocation)
                ? Math.Min(remainingBudget, allocationRemaining[allocation]) : 0;
        }
        int MinimumCountFor(SpatialSpawnCandidate point)
        {
            if (!targetPositions.TryGetValue(point.Id, out BattlefieldPoint targetPosition) ||
                !snapshot.Grid.TryGetCellAt(targetPosition, out _))
                return 0;
            int budget = BudgetFor(point);
            int minimum = int.MaxValue;
            foreach (EnemySquadPreset preset in presets)
                if (costs[preset] <= budget && preset.TotalWeight <= remainingCount)
                    minimum = Math.Min(minimum, preset.TotalWeight);
            return minimum == int.MaxValue ? 0 : minimum;
        }

        var planned = new List<SpawnInstruction>();
        var affordable = new List<EnemySquadPreset>(presets.Count);
        while (remainingBudget > 0 && remainingCount > 0 &&
               session.TryGetNextPoint(MinimumCountFor, out SpatialSpawnCandidate point))
        {
            affordable.Clear();
            int budget = BudgetFor(point);
            foreach (EnemySquadPreset preset in presets)
                if (costs[preset] <= budget && preset.TotalWeight <= remainingCount)
                    affordable.Add(preset);

            TargetAttackType attackType = targetAttackTypes.TryGetValue(point.Id, out TargetAttackType knownType)
                ? knownType : TargetAttackType.Unknown;
            SpawnAreaContext area = session.GetAreaContext(
                point, targetPositions[point.Id], totalEnemyCount, playerPositions, attackType);
            if (!SquadSelectionPolicy.TrySelect(affordable, area, settings, random, out EnemySquadPreset selected))
                break;

            onSelected?.Invoke(point.Id, area, affordable, selected);

            var enemies = new List<EnemySpawnEntry>(selected.Composition.Count);
            foreach (EnemySquadCompositionEntry entry in selected.Composition)
                enemies.Add(new EnemySpawnEntry(entry.EnemyId, entry.Weight));
            planned.Add(new SpawnInstruction(selected.SquadOrder, point.Id, enemies));

            remainingBudget -= costs[selected];
            remainingCount -= selected.TotalWeight;
            allocationRemaining[allocationsByPoint[point.Id]] -= costs[selected];
            session.Reserve(point, selected.TotalWeight);
        }
        LastCandidates = session.GetObservations();
        instructions = planned.AsReadOnly();
        return planned.Count > 0;
    }

    /// <summary>
    /// 호출자가 지정한 PoC 분대 요청을 현재 스폰 지점 상태에 맞춰 계획합니다.
    /// </summary>
    public bool TryPlan(
        FixedPresetSpawnRequest request,
        SpawnPointRegistry spawnPoints,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        return TryPlan(request, spawnPoints, null, null, null, out instructions);
    }

    /// <summary>현재 Match의 공간 스냅샷으로 후보를 걸러 분대 세트를 배분합니다.</summary>
    public bool TryPlan(
        FixedPresetSpawnRequest request,
        SpawnPointRegistry spawnPoints,
        BattlefieldSpatialSnapshot snapshot,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        return TryPlan(request, spawnPoints, snapshot, null, null, out instructions);
    }

    /// <summary>권위 측 환경을 실제 Enemy 정책 데이터로 소비한 뒤 기존 공간 배분을 수행합니다.</summary>
    public bool TryPlan(FixedPresetSpawnRequest request, SpawnPointRegistry points, BattlefieldSpatialSnapshot battlefield,
        MatchEnvironmentSnapshot environment, EncounterEnvironmentProfile profile, out IReadOnlyList<SpawnInstruction> instructions)
    {
        LastBudgetAllocations = Array.Empty<SpawnBudgetAllocation>();
        LastEnvironment = environment; LastEnvironmentPolicy = "neutral";
        if (environment != null && (battlefield == null || environment.MatchId != battlefield.MatchId))
        { LastCandidates = Array.Empty<SpawnDecisionSnapshot.Candidate>(); instructions = Array.Empty<SpawnInstruction>(); return false; }
        if (profile != null)
        {
            request = profile.Select(request, environment, out string policy);
            LastEnvironmentPolicy = policy;
        }
        return TryBuildInstructions(request, points, battlefield, out instructions);
    }

    /// <summary>명시 지점 또는 World 영역을 같은 공간 배분 정책과 분대 구성으로 계획합니다.</summary>
    public bool TryPlanOrigin(FixedPresetSpawnRequest request, SpawnPointRegistry points, BattlefieldSpatialSnapshot battlefield,
        MatchEnvironmentSnapshot environment, EncounterEnvironmentProfile profile, EncounterSpawnPlacement placement,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        instructions = Array.Empty<SpawnInstruction>();
        LastBudgetAllocations = Array.Empty<SpawnBudgetAllocation>();
        LastEnvironment = environment; LastEnvironmentPolicy = "neutral";
        LastCandidates = Array.Empty<SpawnDecisionSnapshot.Candidate>();
        if (battlefield == null || placement == null || (environment != null && environment.MatchId != battlefield.MatchId)) return false;
        if (profile != null) { request = profile.Select(request, environment, out var rule); LastEnvironmentPolicy = rule; }
        if (!TryReadPreset(request, out var preset, out int sets)) return false;
        var candidates = new List<SpatialSpawnCandidate>();
        var areas = new Dictionary<string, SpawnArea>(StringComparer.Ordinal);
        bool authored = placement.Origin.Kind == SpawnOriginKind.AuthoredPoint;
        if (authored)
        {
            if (points == null || !points.TryGetSpawnPoint(placement.Origin.PointId.Value, out var point) || !point.CanSpawnEnemies) return false;
            candidates.Add(new SpatialSpawnCandidate(point.SpawnPointId, new BattlefieldPoint(point.Position.x, point.Position.y)));
        }
        else
        {
            var area = placement.Area; var grid = battlefield.Grid.Configuration;
            double left = Math.Max(grid.MinimumX, area.Center.X - area.Width / 2), right = Math.Min(grid.MaximumX, area.Center.X + area.Width / 2);
            double bottom = Math.Max(grid.MinimumY, area.Center.Y - area.Height / 2), top = Math.Min(grid.MaximumY, area.Center.Y + area.Height / 2);
            // Grid의 최댓값은 마지막 셀에 속합니다. 별도 나눗셈은 점/선 요청에서 서로 다른 경계를 만들 수 있습니다.
            if (right < left || top < bottom ||
                !grid.TryMapPoint(new BattlefieldPoint(left, bottom), out int x0, out int y0) ||
                !grid.TryMapPoint(new BattlefieldPoint(right, top), out int x1, out int y1)) return false;
            if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > 4096) return false;
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                double minX = Math.Max(left, grid.MinimumX + x * grid.CellWidth), maxX = Math.Min(right, grid.MinimumX + (x + 1) * grid.CellWidth);
                double minY = Math.Max(bottom, grid.MinimumY + y * grid.CellHeight), maxY = Math.Min(top, grid.MinimumY + (y + 1) * grid.CellHeight);
                if ((area.Width > 0 && maxX <= minX) || (area.Height > 0 && maxY <= minY)) continue;
                string key = $"world-cell/{x}/{y}";
                var clipped = new SpawnArea(new BattlefieldPoint((minX + maxX) / 2, (minY + maxY) / 2), maxX - minX, maxY - minY, area.VisualZ);
                areas.Add(key, clipped); candidates.Add(new SpatialSpawnCandidate(key, clipped.Center));
            }
        }
        var allocations = _spatialPolicy.Allocate(battlefield, candidates, sets, preset.TotalWeight, out var decisions);
        LastCandidates = decisions;
        var plans = new List<SpawnInstruction>();
        foreach (var allocation in allocations)
        {
            if (allocation.Sets == 0) continue;
            var entries = new List<EnemySpawnEntry>();
            foreach (var entry in preset.Composition) entries.Add(new EnemySpawnEntry(entry.EnemyId, entry.Weight * allocation.Sets));
            plans.Add(new SpawnInstruction(request.SquadOrder, authored ? allocation.SpawnPointId : "",
                entries, authored ? placement : new EncounterSpawnPlacement(placement.Origin, areas[allocation.SpawnPointId])));
        }
        instructions = plans.AsReadOnly(); return plans.Count > 0;
    }

    /// <summary>
    /// 고정 편성의 구성과 비용을 검사하고 예산·마릿수 안에 들어가는 완전한 세트 수를 구한다.
    /// </summary>
    private bool TryReadPreset(FixedPresetSpawnRequest request, out EnemySquadPreset preset, out int sets)
    {
        sets = 0; preset = null;
        if (string.IsNullOrWhiteSpace(request.SquadOrder) || request.MaxCount <= 0 || !TryGetPreset(request.SquadOrder, out preset) ||
            preset.Composition == null || preset.Composition.Count == 0 || preset.TotalWeight <= 0) return false;
        foreach (var entry in preset.Composition)
            if (entry == null || string.IsNullOrWhiteSpace(entry.EnemyId) || entry.Weight <= 0) return false;
        int cost = preset.CalculateCost(_enemyCatalog);
        if (cost <= 0) return false;
        sets = Math.Min(request.MaxCount / preset.TotalWeight, request.SpawnBudget / cost);
        return sets > 0;
    }

    /// <summary>
    /// 현재 적을 생성할 수 있는 모든 지점에 완전한 프리셋 세트를 배분한다.
    /// 점령된 지점은 배분 대상에서 제외되므로 요청의 생성 한도를 소비하지 않는다.
    /// 비용 예산과 마릿수 상한을 모두 만족하는 완전한 프리셋 세트만 생성한다.
    /// </summary>
    /// <param name="spawnRequest">분대 명령과 전체 생성 상한, 비용 예산.</param>
    /// <param name="spawnPoints">현재 지점의 점령 상태를 조회할 레지스트리.</param>
    /// <param name="instructions">스폰 지점별로 확정된 생성 명령 목록.</param>
    /// <returns>적어도 한 지점에 한 구성 세트를 배정했으면 <see langword="true"/>, 아니면 <see langword="false"/>.</returns>
    private bool TryBuildInstructions(
        FixedPresetSpawnRequest spawnRequest,
        SpawnPointRegistry spawnPoints,
        BattlefieldSpatialSnapshot snapshot,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        instructions = Array.Empty<SpawnInstruction>();
        var observations = new List<SpawnDecisionSnapshot.Candidate>();
        LastBudgetAllocations = Array.Empty<SpawnBudgetAllocation>();
        LastCandidates = observations.AsReadOnly();
        if (spawnPoints != null)
            foreach (SpawnPoint point in spawnPoints.GetRegisteredSpawnPoints())
                if (point != null && (!point.CanSpawnEnemies || string.IsNullOrWhiteSpace(point.SpawnPointId)))
                    observations.Add(new SpawnDecisionSnapshot.Candidate(point.SpawnPointId,
                        new BattlefieldPoint(point.Position.x,point.Position.y),-1,-1,0,0,0,false,
                        !point.isActiveAndEnabled ? "Inactive point" : !point.CanSpawnEnemies ? "Spawn disabled / captured" : "Missing ID"));

        if (spawnPoints == null ||
            string.IsNullOrWhiteSpace(spawnRequest.SquadOrder) ||
            spawnRequest.MaxCount <= 0 ||
            spawnRequest.SpawnBudget <= 0 ||
            !HasCatalog ||
            !TryGetPreset(spawnRequest.SquadOrder, out EnemySquadPreset squadPreset) ||
            squadPreset.Composition == null || squadPreset.Composition.Count == 0)
            return false;

        int totalWeight = squadPreset.TotalWeight;
        if (totalWeight <= 0)
            return false;

        // 잘못된 프리셋은 어느 지점에도 부분적으로 배정하지 않는다.
        foreach (EnemySquadCompositionEntry entry in squadPreset.Composition)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.EnemyId) ||
                entry.Weight <= 0)
                return false;
        }

        int presetCost = squadPreset.CalculateCost(_enemyCatalog);
        if (presetCost <= 0)
            return false;

        // 비용과 마릿수 제한을 모두 만족하는 완전한 편성만 생성한다.
        int setsByBudget = spawnRequest.SpawnBudget / presetCost;
        int setsByCount = spawnRequest.MaxCount / totalWeight;
        int totalSets = Math.Min(setsByBudget, setsByCount);
        IReadOnlyList<SpawnPoint> availablePoints = spawnPoints.GetAvailableSpawnPoints();
        if (totalSets == 0 || availablePoints.Count == 0)
            return false;

        IReadOnlyList<SpatialSpawnAllocation> allocations;
        if (snapshot != null)
        {
            var candidates = new List<SpatialSpawnCandidate>(availablePoints.Count);
            foreach (SpawnPoint point in availablePoints)
                candidates.Add(new SpatialSpawnCandidate(
                    point.SpawnPointId,
                    new BattlefieldPoint(point.Position.x, point.Position.y)));
            allocations = _spatialPolicy.Allocate(snapshot, candidates, totalSets, totalWeight, out var evaluated);
            observations.AddRange(evaluated);
        }
        else
        {
            // 기존 독립 스폰 테스트 경로는 공간 입력이 없어 균등 배분을 유지한다.
            int setsPerPoint = totalSets / availablePoints.Count;
            int extraSets = totalSets % availablePoints.Count;
            var evenlyDistributed = new List<SpatialSpawnAllocation>();
            for (int index = 0; index < availablePoints.Count; index++)
                evenlyDistributed.Add(new SpatialSpawnAllocation(
                    availablePoints[index].SpawnPointId,
                    setsPerPoint + (index < extraSets ? 1 : 0)));
            allocations = evenlyDistributed;
        }

        List<SpawnInstruction> planned = new();
        foreach (SpatialSpawnAllocation allocation in allocations)
        {
            if (allocation.Sets == 0)
                continue;

            List<EnemySpawnEntry> enemies = new(squadPreset.Composition.Count);
            foreach (EnemySquadCompositionEntry entry in squadPreset.Composition)
                enemies.Add(new EnemySpawnEntry(entry.EnemyId, entry.Weight * allocation.Sets));

            planned.Add(new SpawnInstruction(
                spawnRequest.SquadOrder,
                allocation.SpawnPointId,
                enemies));
        }

        instructions = planned.AsReadOnly();
        return planned.Count > 0;
    }

    private bool TryGetPreset(string squadOrder, out EnemySquadPreset squadPreset)
    {
        squadPreset = null;
        return _catalog != null &&
               _catalog.TryGetPreset(squadOrder, out squadPreset);
    }
}

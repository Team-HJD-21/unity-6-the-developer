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
    public IReadOnlyList<SpawnDecisionSnapshot.Candidate> LastCandidates { get; private set; } = Array.Empty<SpawnDecisionSnapshot.Candidate>();

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
    public bool TryPlan(AutomaticSpawnRequest request,
        IReadOnlyList<SpatialSpawnCandidate> spawnPoints, BattlefieldSpatialSnapshot snapshot,
        IReadOnlyDictionary<string, BattlefieldPoint> targetPositions,
        int totalEnemyCount,
        SquadSelectionSettings settings, IRandomSource random,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        instructions = Array.Empty<SpawnInstruction>();
        LastCandidates = Array.Empty<SpawnDecisionSnapshot.Candidate>();
        if (!HasCatalog || request.SpawnBudget <= 0 || request.MaxCount <= 0 ||
            snapshot?.Grid == null || spawnPoints == null || targetPositions == null || totalEnemyCount < 0 ||
            settings == null || !settings.IsValid ||
            random == null || !random.IsAvailable)
            return false;

        // 비용은 요청마다 한 번만 읽고, 카탈로그 순서와 무관하게 동일한 난수 흐름을 사용한다.
        var presets = new List<EnemySquadPreset>();
        var costs = new Dictionary<EnemySquadPreset, int>();
        var orders = new HashSet<string>(StringComparer.Ordinal);
        foreach (EnemySquadPreset preset in GetSpawnablePresets(request.SpawnBudget, request.MaxCount))
        {
            if (string.IsNullOrWhiteSpace(preset.SquadOrder) || preset.SelectionWeight <= 0 ||
                !Enum.IsDefined(typeof(EnemySquadRole), preset.Role) || !orders.Add(preset.SquadOrder))
                continue;
            presets.Add(preset);
            costs.Add(preset, preset.CalculateCost(_enemyCatalog));
        }
        if (presets.Count == 0) return false;
        presets.Sort((left, right) => StringComparer.Ordinal.Compare(left.SquadOrder, right.SquadOrder));

        var allocationsByPoint = new Dictionary<string, SpawnBudgetAllocation>(StringComparer.Ordinal);
        var allocationRemaining = new Dictionary<SpawnBudgetAllocation, int>();
        if (request.BudgetAllocations != null)
            foreach (SpawnBudgetAllocation allocation in request.BudgetAllocations)
            {
                allocationRemaining.Add(allocation, allocation.Budget);
                foreach (string id in allocation.SpawnPointIds) allocationsByPoint.Add(id, allocation);
            }

        int remainingBudget = request.SpawnBudget;
        int remainingCount = request.MaxCount;
        int BudgetFor(SpatialSpawnCandidate point)
        {
            if (request.BudgetAllocations == null) return remainingBudget;
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

        var session = _spatialPolicy.BeginPlanning(snapshot, spawnPoints, settings);
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

            SpawnAreaContext area = session.GetAreaContext(point, targetPositions[point.Id], totalEnemyCount);
            if (!SquadSelectionPolicy.TrySelect(affordable, area, settings, random, out EnemySquadPreset selected))
                break;

            var enemies = new List<EnemySpawnEntry>(selected.Composition.Count);
            foreach (EnemySquadCompositionEntry entry in selected.Composition)
                enemies.Add(new EnemySpawnEntry(entry.EnemyId, entry.Weight));
            planned.Add(new SpawnInstruction(selected.SquadOrder, point.Id, enemies));

            remainingBudget -= costs[selected];
            remainingCount -= selected.TotalWeight;
            if (request.BudgetAllocations != null)
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
        SpawnRequest request,
        SpawnPointRegistry spawnPoints,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        return TryBuildInstructions(request, spawnPoints, null, out instructions);
    }

    /// <summary>현재 Match의 공간 스냅샷으로 후보를 걸러 분대 세트를 배분합니다.</summary>
    public bool TryPlan(
        SpawnRequest request,
        SpawnPointRegistry spawnPoints,
        BattlefieldSpatialSnapshot snapshot,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        return TryBuildInstructions(request, spawnPoints, snapshot, out instructions);
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
        SpawnRequest spawnRequest,
        SpawnPointRegistry spawnPoints,
        BattlefieldSpatialSnapshot snapshot,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        instructions = Array.Empty<SpawnInstruction>();
        var observations = new List<SpawnDecisionSnapshot.Candidate>();
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

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
    // 분대 프리셋과 상한은 임시값이며, 공간별 난이도·스폰율 조정은 후속 정책이다.
    private readonly EnemySquadPresetCatalog _catalog;
    private readonly SpawnPointSpatialPolicy _spatialPolicy = new SpawnPointSpatialPolicy();
    public IReadOnlyList<SpawnDecisionSnapshot.Candidate> LastCandidates { get; private set; } = Array.Empty<SpawnDecisionSnapshot.Candidate>();
    public MatchEnvironmentSnapshot LastEnvironment { get; private set; }
    public string LastEnvironmentPolicy { get; private set; } = "neutral";

    /// <summary>
    /// 분대 프리셋을 조회할 카탈로그가 로드되었는지 반환한다.
    /// </summary>
    public bool HasCatalog => _catalog != null;

    /// <summary>Executor가 실제로 생성한 적만 Match별 공간 배분 이력에 반영합니다.</summary>
    public void RecordExecuted(BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpawnDecisionSnapshot.Actual> actuals)
        => _spatialPolicy.RecordExecuted(snapshot, actuals);

    /// <summary>
    /// 기본 카탈로그를 한 번 로드해 Planner를 만든다.
    /// </summary>
    public SpawnCompositionPlanner() : this(EnemySquadPresetCatalog.Load())
    {
    }

    /// <summary>
    /// Core 구성 계층이나 테스트에서 이미 로드한 카탈로그를 전달받는다.
    /// </summary>
    /// <param name="catalog">분대 명령으로 조회할 프리셋 카탈로그.</param>
    public SpawnCompositionPlanner(EnemySquadPresetCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// 현재 PoC 기준으로 분대 유형과 생성 상한을 정하고, 스폰 가능한 지점별 명령을 만든다.
    /// Normal과 최대 10마리는 임시 판단값이며 실제 생성 수는 프리셋의 완전한 세트 수에 따라 결정된다.
    /// </summary>
    /// <param name="spawnPoints">현재 지점의 점령 상태를 조회할 레지스트리.</param>
    /// <param name="instructions">스폰 지점별로 확정된 생성 명령 목록.</param>
    /// <returns>적어도 한 지점에 한 구성 세트를 배정했으면 <see langword="true"/>, 아니면 <see langword="false"/>.</returns>
    public bool TryPlan(
        SpawnPointRegistry spawnPoints,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        return TryPlan(new SpawnRequest("Normal", 10), spawnPoints, out instructions);
    }

    /// <summary>
    /// 호출자가 지정한 PoC 분대 요청을 현재 스폰 지점 상태에 맞춰 계획합니다.
    /// </summary>
    public bool TryPlan(
        SpawnRequest request,
        SpawnPointRegistry spawnPoints,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        return TryPlan(request, spawnPoints, null, null, null, out instructions);
    }

    /// <summary>현재 Match의 공간 스냅샷으로 후보를 걸러 분대 세트를 배분합니다.</summary>
    public bool TryPlan(
        SpawnRequest request,
        SpawnPointRegistry spawnPoints,
        BattlefieldSpatialSnapshot snapshot,
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        return TryPlan(request, spawnPoints, snapshot, null, null, out instructions);
    }

    /// <summary>권위 측 환경을 실제 Enemy 정책 데이터로 소비한 뒤 기존 공간 배분을 수행합니다.</summary>
    public bool TryPlan(SpawnRequest request, SpawnPointRegistry points, BattlefieldSpatialSnapshot battlefield,
        MatchEnvironmentSnapshot environment, EncounterEnvironmentProfile profile, out IReadOnlyList<SpawnInstruction> instructions)
    {
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

    /// <summary>
    /// 현재 적을 생성할 수 있는 모든 지점에 완전한 프리셋 세트를 배분한다.
    /// 점령된 지점은 배분 대상에서 제외되므로 요청의 생성 한도를 소비하지 않는다.
    /// 생성 한도보다 작은 프리셋 잔여 수량은 생성하지 않는다.
    /// </summary>
    /// <param name="spawnRequest">분대 명령과 전체 생성 상한.</param>
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

        // 요청 상한을 프리셋 한 세트의 크기로 나눠 배정 가능한 완전한 세트 수를 구한다.
        int totalSets = spawnRequest.MaxCount / totalWeight;
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

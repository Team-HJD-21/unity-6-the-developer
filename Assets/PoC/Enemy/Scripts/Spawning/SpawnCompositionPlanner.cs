using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;

/// <summary>
/// 분대 프리셋의 구성 비율과 현재 사용 가능한 스폰 지점을 바탕으로 생성 명령을 만든다.
/// 생성 자체는 담당하지 않으며, 점령된 지점에는 생성 수를 배정하지 않는다.
/// </summary>
public sealed class SpawnCompositionPlanner
{
    private const string PoCSquadOrder = "Normal";
    private const int PoCMaxCount = 10;

    // TODO: Encounter의 전선/Grid 정보와 Spawn Policy가 준비되면 고정된 분대 유형과 생성 상한을 상황별 계산으로 교체한다.
    private readonly EnemySquadPresetCatalog _catalog;

    /// <summary>
    /// 분대 프리셋을 조회할 카탈로그가 로드되었는지 반환한다.
    /// </summary>
    public bool HasCatalog => _catalog != null;

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
        SpawnRequest request = new SpawnRequest(PoCSquadOrder, PoCMaxCount);
        return TryBuildInstructions(request, spawnPoints, out instructions);
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
        out IReadOnlyList<SpawnInstruction> instructions)
    {
        instructions = Array.Empty<SpawnInstruction>();

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

        // 세트 수를 균등하게 나누고 남는 세트는 등록 순서상 앞쪽 지점부터 배정한다. (추후 점수 계산을 통해 세트 수 비균등)
        int setsPerPoint = totalSets / availablePoints.Count;
        int extraSets = totalSets % availablePoints.Count;
        List<SpawnInstruction> planned = new();

        for (int pointIndex = 0; pointIndex < availablePoints.Count; pointIndex++)
        {
            int pointSets = setsPerPoint + (pointIndex < extraSets ? 1 : 0);
            if (pointSets == 0)
                continue;

            List<EnemySpawnEntry> enemies = new(squadPreset.Composition.Count);
            foreach (EnemySquadCompositionEntry entry in squadPreset.Composition)
                enemies.Add(new EnemySpawnEntry(entry.EnemyId, entry.Weight * pointSets));

            planned.Add(new SpawnInstruction(
                spawnRequest.SquadOrder,
                availablePoints[pointIndex].SpawnPointId,
                enemies));
        }

        instructions = planned.AsReadOnly();
        return true;
    }

    private bool TryGetPreset(string squadOrder, out EnemySquadPreset squadPreset)
    {
        squadPreset = null;
        return _catalog != null &&
               _catalog.TryGetPreset(squadOrder, out squadPreset);
    }
}

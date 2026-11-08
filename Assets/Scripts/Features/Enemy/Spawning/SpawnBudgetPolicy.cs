using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>최소 편성 비용을 확보하고 타깃 주변 상황에 따라 지역 예산을 배분한다.</summary>
public static class SpawnBudgetPolicy
{
    public sealed class Area
    {
        public string Id { get; }
        public IReadOnlyList<string> SpawnPointIds { get; }
        public SpawnAreaContext Situation { get; }
        public int MinimumCost { get; }
        public long PreviousSpawnCount { get; }

        public Area(string id, IReadOnlyList<string> spawnPointIds, SpawnAreaContext situation,
            int minimumCost, long previousSpawnCount)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A budget area ID is required.", nameof(id));
            if (spawnPointIds == null || spawnPointIds.Count == 0)
                throw new ArgumentException("A budget area requires spawn points.", nameof(spawnPointIds));
            if (minimumCost <= 0) throw new ArgumentOutOfRangeException(nameof(minimumCost));
            if (previousSpawnCount < 0) throw new ArgumentOutOfRangeException(nameof(previousSpawnCount));
            Id = id;
            SpawnPointIds = new List<string>(spawnPointIds).AsReadOnly();
            Situation = situation;
            MinimumCost = minimumCost;
            PreviousSpawnCount = previousSpawnCount;
        }
    }

    private sealed class AreaBudgetAllocation
    {
        public Area Area;
        public long Weight;
        public int Budget;
        public decimal Fraction;
    }

    public static long CalculateWeight(SpawnAreaContext area, SquadSelectionSettings settings)
    {
        long weight = settings.baseBudgetWeight;
        if (area.NearbyEnemyPercentage <= settings.sparseEnemyPercentage)
            weight += settings.sparseBudgetBonus;
        if (area.HasNearbyPlayer)
            weight += settings.playerBudgetBonus;
        return weight;
    }

    /// <summary>예산이 부족하면 비중과 기존 생성 이력 순으로 투입할 지역을 선택한다.</summary>
    public static IReadOnlyList<SpawnBudgetAllocation> Allocate(int totalBudget,
        IReadOnlyList<Area> areas, SquadSelectionSettings settings)
    {
        if (areas == null) throw new ArgumentNullException(nameof(areas));
        if (settings == null || !settings.IsValid)
            throw new ArgumentException("Invalid spawn budget settings.", nameof(settings));
        if (totalBudget <= 0) return Array.Empty<SpawnBudgetAllocation>();

        var candidates = areas.Select(area => new AreaBudgetAllocation
            {
                Area = area,
                Weight = CalculateWeight(area.Situation, settings)
            })
            .OrderByDescending(allocation => allocation.Weight)
            .ThenBy(allocation => allocation.Area.PreviousSpawnCount)
            .ThenBy(allocation => allocation.Area.Id, StringComparer.Ordinal);

        int remaining = totalBudget;
        var active = new List<AreaBudgetAllocation>();
        foreach (AreaBudgetAllocation allocation in candidates)
        {
            if (allocation.Area.MinimumCost > remaining) continue;
            allocation.Budget = allocation.Area.MinimumCost;
            remaining -= allocation.Budget;
            active.Add(allocation);
        }
        if (active.Count == 0) return Array.Empty<SpawnBudgetAllocation>();

        long totalWeight = active.Sum(allocation => allocation.Weight);
        int distributed = 0;
        foreach (AreaBudgetAllocation allocation in active)
        {
            decimal amount = (decimal)remaining * allocation.Weight / totalWeight;
            int whole = (int)decimal.Floor(amount);
            allocation.Budget += whole;
            allocation.Fraction = amount - whole;
            distributed += whole;
        }

        int remainder = remaining - distributed;
        foreach (AreaBudgetAllocation allocation in active
                     .OrderByDescending(item => item.Fraction)
                     .ThenByDescending(item => item.Weight)
                     .ThenBy(item => item.Area.PreviousSpawnCount)
                     .ThenBy(item => item.Area.Id, StringComparer.Ordinal))
        {
            if (remainder == 0) break;
            allocation.Budget++;
            remainder--;
        }

        return active.Select(allocation => new SpawnBudgetAllocation(
            allocation.Area.Id, allocation.Budget, allocation.Area.SpawnPointIds)).ToArray();
    }
}

using System;
using System.Collections.Generic;

/// <summary>예산을 공유하는 스폰 지점 묶음. 한 지점 또는 여러 지점으로 구성할 수 있다.</summary>
public sealed class SpawnBudgetAllocation
{
    public string Id { get; }
    public int Budget { get; }
    public IReadOnlyList<string> SpawnPointIds { get; }

    public SpawnBudgetAllocation(string id, int budget, IReadOnlyList<string> spawnPointIds)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A budget allocation ID is required.", nameof(id));
        if (budget < 0) throw new ArgumentOutOfRangeException(nameof(budget));
        if (spawnPointIds == null) throw new ArgumentNullException(nameof(spawnPointIds));
        var ids = new List<string>(spawnPointIds.Count);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string pointId in spawnPointIds)
        {
            if (string.IsNullOrWhiteSpace(pointId) || !unique.Add(pointId))
                throw new ArgumentException("Budget allocation point IDs must be valid and unique.", nameof(spawnPointIds));
            ids.Add(pointId);
        }
        Id = id;
        Budget = budget;
        SpawnPointIds = ids.AsReadOnly();
    }
}

/// <summary>특정 편성을 지정하지 않는 자동 계획의 요청 상한과 선택적 지역 예산.</summary>
public readonly struct AutomaticSpawnRequest
{
    public int SpawnBudget { get; }
    public int MaxCount { get; }
    /// <summary>null이면 요청 전체 예산만 적용한다. 지정하면 등록된 지점에만 해당 묶음 예산을 적용한다.</summary>
    public IReadOnlyList<SpawnBudgetAllocation> BudgetAllocations { get; }

    public AutomaticSpawnRequest(int spawnBudget, int maxCount,
        IReadOnlyList<SpawnBudgetAllocation> budgetAllocations = null)
    {
        SpawnBudget = spawnBudget;
        MaxCount = maxCount;
        if (budgetAllocations == null)
        {
            BudgetAllocations = null;
            return;
        }
        var allocations = new List<SpawnBudgetAllocation>(budgetAllocations.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var points = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpawnBudgetAllocation allocation in budgetAllocations)
        {
            if (allocation == null || !ids.Add(allocation.Id))
                throw new ArgumentException("Budget allocation IDs must be valid and unique.", nameof(budgetAllocations));
            foreach (string pointId in allocation.SpawnPointIds)
                if (!points.Add(pointId))
                    throw new ArgumentException("A spawn point cannot share multiple budget allocations.", nameof(budgetAllocations));
            allocations.Add(allocation);
        }
        BudgetAllocations = allocations.AsReadOnly();
    }
}

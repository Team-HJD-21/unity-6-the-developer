using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Domain;

/// <summary>
/// 점유율 기반 총예산과 지역별 분배의 합계·최소 비용 조건을 검사한다.
/// </summary>
public sealed class SpawnBudgetPolicyTests
{
    [TestCase(RegionPolicyMode.Static, 0, 20)]
    [TestCase(RegionPolicyMode.Static, 1, 40)]
    [TestCase(RegionPolicyMode.Static, 2, 60)]
    [TestCase(RegionPolicyMode.Static, 3, 80)]
    [TestCase(RegionPolicyMode.Static, 4, 100)]
    [TestCase(RegionPolicyMode.Dynamic, 0, 20)]
    [TestCase(RegionPolicyMode.Dynamic, 1, 40)]
    [TestCase(RegionPolicyMode.Dynamic, 2, 60)]
    [TestCase(RegionPolicyMode.Dynamic, 3, 80)]
    [TestCase(RegionPolicyMode.Dynamic, 4, 100)]
    public void TotalBudget_InterpolatesFromCapturedCells(RegionPolicyMode mode, int captured, int expected)
    {
        var region = Region(captured, mode);
        int budget = SpawnBudgetPolicy.CalculateTotalBudget(region, 20, 100);
        Assert.That(budget, Is.EqualTo(expected));
    }

    [Test]
    public void TotalBudget_FloorsFractionAndPreservesIntegerLimits()
    {
        var geometry = new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 3, 1, 3, 1);
        var region = RegionGridSnapshot.FromCapturedCells(new MatchId("budget-test"), Guid.NewGuid(), 1,
            RegionPolicyMode.Dynamic, new RegionGridDefinition(geometry), new[] { 0 });
        Assert.That(SpawnBudgetPolicy.CalculateTotalBudget(region, 20, 100), Is.EqualTo(46));
        Assert.That(SpawnBudgetPolicy.CalculateTotalBudget(Region(4), 1, int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(SpawnBudgetPolicy.CalculateTotalBudget(Region(2, RegionPolicyMode.Static), 20, 20), Is.EqualTo(20));
    }

    [TestCase(0, 100)]
    [TestCase(20, 19)]
    public void TotalBudget_RejectsInvalidLimits(int minimum, int maximum)
    {
        Assert.Throws<ArgumentException>(() => SpawnBudgetPolicy.CalculateTotalBudget(Region(0), minimum, maximum));
        Assert.Throws<ArgumentException>(() => SpawnBudgetPolicy.CalculateTotalBudget(Region(0, RegionPolicyMode.Static), minimum, maximum));
    }

    [Test]
    public void TotalBudget_MissingSnapshotIsNotTreatedAsZeroCapture()
    {
        Assert.Throws<ArgumentNullException>(() => SpawnBudgetPolicy.CalculateTotalBudget(null, 20, 100));
    }

    [TestCase(201, false, 1)]
    [TestCase(200, false, 2)]
    [TestCase(201, true, 3)]
    [TestCase(200, true, 4)]
    public void Weight_UsesPopulationThresholdAndPlayer(int nearby, bool player, long expected)
    {
        var situation = new SpawnAreaContext(0, nearby, 1000, player);
        Assert.That(SpawnBudgetPolicy.CalculateWeight(situation, new SquadSelectionSettings()), Is.EqualTo(expected));
    }

    [Test]
    public void Allocate_ReservesMinimumsThenDistributesRemainders()
    {
        var areas = new[] { Area("a", true, true), Area("b", true, false), Area("c", false, false) };
        var budgets = SpawnBudgetPolicy.Allocate(20, areas, new SquadSelectionSettings()).ToDictionary(a => a.Id, a => a.Budget);
        Assert.That(budgets["a"], Is.EqualTo(9));
        Assert.That(budgets["b"], Is.EqualTo(6));
        Assert.That(budgets["c"], Is.EqualTo(5));
        Assert.That(budgets.Values.Sum(), Is.EqualTo(20));
    }

    [Test]
    public void Allocate_ScarceBudgetFundsCompleteSquadsByPriorityAndHistory()
    {
        var areas = new[]
        {
            Area("old", true, true, previous: 10),
            Area("unused", true, true),
            Area("low", false, false)
        };
        var allocations = SpawnBudgetPolicy.Allocate(5, areas, new SquadSelectionSettings());
        Assert.That(allocations.Count, Is.EqualTo(1));
        Assert.That(allocations[0].Id, Is.EqualTo("unused"));
        Assert.That(allocations[0].Budget, Is.EqualTo(5));
    }

    [Test]
    public void Allocate_SpawnPointCountDoesNotIncreaseAreaBudget()
    {
        var settings = new SquadSelectionSettings();
        var first = SpawnBudgetPolicy.Allocate(20,
            new[] { Area("a", true, false), Area("b", true, false) }, settings);
        var expanded = SpawnBudgetPolicy.Allocate(20,
            new[] { Area("a", true, false, ids: new[] { "a1", "a2", "a3" }), Area("b", true, false) }, settings);
        Assert.That(expanded.Select(a => a.Budget), Is.EqualTo(first.Select(a => a.Budget)));
        Assert.That(expanded[0].SpawnPointIds.Count, Is.EqualTo(3));
    }

    [TestCase(0)]
    [TestCase(3)]
    public void Allocate_InsufficientBudgetDoesNotCreateUnusableAllocations(int budget)
    {
        Assert.That(SpawnBudgetPolicy.Allocate(budget,
            new[] { Area("a", true, false) }, new SquadSelectionSettings()), Is.Empty);
    }

    [Test]
    public void Allocate_SkipsUnaffordableAreaAndKeepsIntegerBudgetAtLargeValues()
    {
        var settings = new SquadSelectionSettings();
        var areas = new[] { Area("expensive", true, true, minimum: 30), Area("cheap", false, false) };
        var allocations = SpawnBudgetPolicy.Allocate(20, areas, settings);
        Assert.That(allocations.Single().Id, Is.EqualTo("cheap"));
        Assert.That(allocations.Single().Budget, Is.EqualTo(20));

        settings.baseBudgetWeight = int.MaxValue;
        settings.sparseBudgetBonus = int.MaxValue;
        settings.playerBudgetBonus = int.MaxValue;
        allocations = SpawnBudgetPolicy.Allocate(int.MaxValue, areas, settings);
        Assert.That(allocations.Sum(a => (long)a.Budget), Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void Allocate_InputOrderDoesNotChangeTieBreaks()
    {
        var areas = new[] { Area("b", true, false), Area("a", true, false) };
        var settings = new SquadSelectionSettings();
        var first = SpawnBudgetPolicy.Allocate(5, areas, settings);
        Array.Reverse(areas);
        var second = SpawnBudgetPolicy.Allocate(5, areas, settings);
        Assert.That(first.Single().Id, Is.EqualTo("a"));
        Assert.That(second.Single().Id, Is.EqualTo("a"));
    }

    private static SpawnBudgetPolicy.Area Area(string id, bool sparse, bool player,
        int minimum = 4, long previous = 0, string[] ids = null)
        => new(id, ids ?? new[] { id }, new SpawnAreaContext(0, sparse ? 0 : 100, 100, player), minimum, previous);

    private static RegionGridSnapshot Region(int captured, RegionPolicyMode mode = RegionPolicyMode.Dynamic)
    {
        var geometry = new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 2, 2, 2, 2);
        return RegionGridSnapshot.FromCapturedCells(new MatchId("budget-test"), Guid.NewGuid(), 1,
            mode, new RegionGridDefinition(geometry), Enumerable.Range(0, captured));
    }
}

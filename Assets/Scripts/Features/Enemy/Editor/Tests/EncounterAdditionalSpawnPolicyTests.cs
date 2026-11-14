// 기존 예산·완성 편성·정규 출현을 유지하며 실제 확장 때만 내부 예고를 선택하는지 검사합니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Enemies.Encounter;

public sealed class EncounterAdditionalSpawnPolicyTests
{
    private static readonly Guid Epoch = Guid.NewGuid();
    private static SpawnInstruction[] Plans() => new[]
    {
        new SpawnInstruction("normal", "point-a", new[] { new EnemySpawnEntry("unit", 3) }),
        new SpawnInstruction("rush", "point-b", new[] { new EnemySpawnEntry("unit", 2) })
    };
    private static RegionRuntime Region()
    {
        var runtime = new RegionRuntime(new MatchId("additional-test"),
            new RegionGridDefinition(new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 20, 20, 4, 4)));
        runtime.TryApply(runtime.Snapshot, Enumerable.Range(0, 8), RegionCaptureUpdateMode.Replace);
        return runtime;
    }
    private static SpawnArea[] Areas() => new[] { new SpawnArea(new BattlefieldPoint(-2.5, -2.5), 4, 4) };
    private static RegionCaptureTrend Trend(double now = 60, int changes = 2, double net = .3)
        => new RegionCaptureTrend(Epoch, now, 60, .5, net, changes);

    [Test]
    public void SelectsOneWholeExistingCompositionWithoutAddingCostOrRemovingAllRegularPlans()
    {
        using var region = Region(); var original = Plans(); var policy = new EncounterAdditionalSpawnPolicy();
        var selected = policy.Select(original, Areas(), region.Snapshot, Trend(), new EncounterAdditionalSpawnSettings(),
            10, p => p.Enemies.Sum(e => e.Count), out var regular, out _);
        Assert.That(selected, Is.Not.Null); Assert.That(regular.Count, Is.EqualTo(1));
        Assert.That(selected.Instruction.SpawnPointId, Is.Empty);
        Assert.That(selected.Instruction.Enemies.Sum(e => e.Count), Is.EqualTo(2));
        Assert.That(regular.Sum(p => p.Enemies.Sum(e => e.Count)) + selected.Cost, Is.EqualTo(5));
        Assert.That(original.Length, Is.EqualTo(2)); Assert.That(original[1].SpawnPointId, Is.EqualTo("point-b"));
    }

    [TestCase(1, .4)]
    [TestCase(2, .1)]
    public void SingleStaticJumpOrInsufficientNetGrowth_LeavesOriginalPlanUnchanged(int changes, double net)
    {
        using var region = Region(); var plans = Plans(); var policy = new EncounterAdditionalSpawnPolicy();
        Assert.That(policy.Select(plans, Areas(), region.Snapshot, Trend(changes: changes, net: net),
            new EncounterAdditionalSpawnSettings(), 10, _ => 2, out var regular, out _), Is.Null);
        Assert.That(regular, Is.SameAs(plans));
    }

    [Test]
    public void MissingAreaOrSinglePlanCannotCreateAnInvisibleOrEntirelyDeferredRequest()
    {
        using var region = Region(); var policy = new EncounterAdditionalSpawnPolicy();
        Assert.That(policy.Select(Plans(), Array.Empty<SpawnArea>(), region.Snapshot, Trend(),
            new EncounterAdditionalSpawnSettings(), 10, _ => 2, out _, out _), Is.Null);
        Assert.That(policy.Select(new[] { Plans()[0] }, Areas(), region.Snapshot, Trend(),
            new EncounterAdditionalSpawnSettings(), 10, _ => 2, out _, out _), Is.Null);
    }

    [Test]
    public void OnlyAcceptedQueueStartsCooldown_AndCheckpointRemainingDelaySurvivesAnEpochChange()
    {
        using var region = Region(); var policy = new EncounterAdditionalSpawnPolicy();
        var settings = new EncounterAdditionalSpawnSettings(); var trend = Trend();
        Assert.That(policy.Select(Plans(), Areas(), region.Snapshot, trend, settings, 10, _ => 2, out _, out _), Is.Not.Null);
        Assert.That(policy.Select(Plans(), Areas(), region.Snapshot, trend, settings, 10, _ => 2, out _, out _), Is.Not.Null);
        policy.RecordQueued(trend, region.Snapshot, 90);
        Assert.That(policy.Select(Plans(), Areas(), region.Snapshot, Trend(now: 61), settings, 10, _ => 2, out _, out _), Is.Null);
        double remaining = policy.Remaining(70); Assert.That(remaining, Is.EqualTo(80));
        var epoch = Guid.NewGuid(); policy.Restore(10, remaining, epoch, region.Snapshot.GridId);
        var restored = new RegionCaptureTrend(epoch, 30, 30, .5, .3, 2);
        Assert.That(policy.Select(Plans(), Areas(), region.Snapshot, restored, settings, 10, _ => 2, out _, out _), Is.Null);
        Assert.That(policy.Remaining(30), Is.EqualTo(60));
    }

    [Test]
    public void UncapturedOrCrossingArea_IsNotTreatedAsAnInternalWarning()
    {
        using var region = Region(); var policy = new EncounterAdditionalSpawnPolicy();
        var outside = new[] { new SpawnArea(new BattlefieldPoint(2.5, 2.5), 2, 2) };
        var crossing = new[] { new SpawnArea(new BattlefieldPoint(-2.5, -2.5), 6, 6) };
        Assert.That(policy.Select(Plans(), outside, region.Snapshot, Trend(), new EncounterAdditionalSpawnSettings(), 10, _ => 2, out _, out _), Is.Null);
        Assert.That(policy.Select(Plans(), crossing, region.Snapshot, Trend(), new EncounterAdditionalSpawnSettings(), 10, _ => 2, out _, out _), Is.Null);
    }

    [Test]
    public void ExpensiveSquadsAreNotSplitToFitTheAdditionalShare()
    {
        using var region = Region(); var policy = new EncounterAdditionalSpawnPolicy();
        Assert.That(policy.Select(Plans(), Areas(), region.Snapshot, Trend(), new EncounterAdditionalSpawnSettings(),
            10, _ => 6, out var regular, out _), Is.Null);
        Assert.That(regular.Count, Is.EqualTo(2));
    }
}

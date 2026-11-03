// World 영역도 기존 셀/전선 배분과 실제 생성 이력을 사용하며 지점으로 위장하지 않는지 검사합니다.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
public sealed class EncounterOriginPlannerTests
{
    [TestCase(16, 24)]
    [TestCase(16, 20)]
    [TestCase(10, 24)]
    [TestCase(4, 16)]
    [TestCase(4, 24)]
    [TestCase(16, 16)]
    public void WorldPointUsesTheSameInclusiveBoundaryAsTheGrid(double x, double y)
    {
        var grid = new BattlefieldGridConfiguration(new BattlefieldPoint(10, 20), 0, 12, 8, 3, 2);
        using var runtime = new TeamHJD.Game.Application.BattlefieldSpatialRuntime(new MatchId("boundary-planner"),
            BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty, grid);
        var planner = new SpawnCompositionPlanner(); var point = new BattlefieldPoint(x, y);
        var origin = EncounterSpawnOrigin.AtWorld(new SpawnArea(point));
        Assert.That(grid.TryMapPoint(point, out int cellX, out int cellY), Is.True);
        Assert.That(planner.TryPlanOrigin(new SpawnRequest("Normal", 10), null, runtime.Snapshot, null, null,
            new EncounterSpawnPlacement(origin, origin.Area), out var plans), Is.True);
        Assert.That(plans.Count, Is.EqualTo(1));
        Assert.That(plans[0].Placement.Area.Center.X, Is.EqualTo(x));
        Assert.That(plans[0].Placement.Area.Center.Y, Is.EqualTo(y));
        Assert.That(planner.LastCandidates.Count, Is.EqualTo(1));
        Assert.That(grid.TryMapPoint(plans[0].Placement.Area.Center, out int actualX, out int actualY), Is.True);
        Assert.That(actualX, Is.EqualTo(cellX)); Assert.That(actualY, Is.EqualTo(cellY));
    }

    [Test]
    public void BoundaryLineUsesLastColumnButPositiveAreaTouchingOnlyAtAnEdgeIsExcluded()
    {
        var grid = new BattlefieldGridConfiguration(new BattlefieldPoint(10, 20), 0, 12, 8, 3, 2);
        using var runtime = new TeamHJD.Game.Application.BattlefieldSpatialRuntime(new MatchId("line-planner"),
            BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty, grid);
        var planner = new SpawnCompositionPlanner();
        var line = EncounterSpawnOrigin.AtWorld(new SpawnArea(new BattlefieldPoint(16, 20), 0, 4));
        Assert.That(planner.TryPlanOrigin(new SpawnRequest("Normal", 10), null, runtime.Snapshot, null, null,
            new EncounterSpawnPlacement(line, line.Area), out var plans), Is.True);
        Assert.That(plans.All(plan => plan.Placement.Area.Center.X == 16 && plan.Placement.Area.Width == 0), Is.True);
        var touch = EncounterSpawnOrigin.AtWorld(new SpawnArea(new BattlefieldPoint(18, 20), 4, 4));
        Assert.That(planner.TryPlanOrigin(new SpawnRequest("Normal", 10), null, runtime.Snapshot, null, null,
            new EncounterSpawnPlacement(touch, touch.Area), out _), Is.False);
    }

    [TestCase(16.001, 20)]
    [TestCase(10, 24.001)]
    [TestCase(3.999, 20)]
    public void WorldPointsOutsideTheGridRemainRejected(double x, double y)
    {
        var grid = new BattlefieldGridConfiguration(new BattlefieldPoint(10, 20), 0, 12, 8, 3, 2);
        using var runtime = new TeamHJD.Game.Application.BattlefieldSpatialRuntime(new MatchId("outside-planner"),
            BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty, grid);
        var planner = new SpawnCompositionPlanner();
        var origin = EncounterSpawnOrigin.AtWorld(new SpawnArea(new BattlefieldPoint(x, y)));
        Assert.That(planner.TryPlanOrigin(new SpawnRequest("Normal", 10), null, runtime.Snapshot, null, null,
            new EncounterSpawnPlacement(origin, origin.Area), out var plans), Is.False);
        Assert.That(plans, Is.Empty);
    }

    [Test]
    public void FrozenSnapshotUsesActualWorldCellServiceRatherThanRepeatedNearestCell()
    {
        using var runtime = new TeamHJD.Game.Application.BattlefieldSpatialRuntime(new MatchId("world-planner"),
            BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty, BattlefieldGridConfiguration.Default);
        var planner = new SpawnCompositionPlanner();
        var origin = EncounterSpawnOrigin.AtWorld(new SpawnArea(new BattlefieldPoint(0, 0), 4, 4));
        var placement = new EncounterSpawnPlacement(origin, origin.Area);
        var used = new Dictionary<string, int>();
        for (int i = 0; i < 24; i++)
        {
            Assert.That(planner.TryPlanOrigin(new SpawnRequest("Normal", 10), null, runtime.Snapshot, null, null, placement, out var plans), Is.True);
            var actuals = new List<SpawnDecisionSnapshot.Actual>();
            foreach (var plan in plans)
            {
                Assert.That(plan.SpawnPointId, Is.Empty);
                var xy = plan.Placement.Area.Center;
                string key = xy.X + ":" + xy.Y;
                int count = plan.Enemies.Sum(entry => entry.Count);
                used[key] = (used.TryGetValue(key, out int previous) ? previous : 0) + count;
                for (int j = 0; j < count; j++) actuals.Add(new SpawnDecisionSnapshot.Actual("", "Slime", i + "/" + j, xy));
            }
            planner.RecordExecuted(runtime.Snapshot, actuals);
        }
        Assert.That(used.Count, Is.EqualTo(16));
        Assert.That(used.Values.Max() - used.Values.Min(), Is.LessThanOrEqualTo(5));
    }
}

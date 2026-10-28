// World 영역도 기존 셀/전선 배분과 실제 생성 이력을 사용하며 지점으로 위장하지 않는지 검사합니다.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
public sealed class EncounterOriginPlannerTests
{
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

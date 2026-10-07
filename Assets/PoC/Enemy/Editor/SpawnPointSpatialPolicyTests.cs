// Stage 1 SpawnPoint 정책이 실제 Grid·Frontline 입력에 따라 달라지는지 검증합니다.
// Editor 테스트는 생성 프리팹이나 NetworkManager 없이 순수 배분 결과를 검사합니다.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

public sealed class SpawnPointSpatialPolicyTests
{
    private SpawnPointSpatialPolicy _policy = new SpawnPointSpatialPolicy();

    [SetUp]
    public void SetUp() => _policy = new SpawnPointSpatialPolicy();

    [Test]
    public void Allocate_RecordsActualRejectionsAndPressureWithoutChangingAllocation()
    {
        var participants=new BattlefieldDynamicSpatialInput(new[]{Player("friendly",0,0)},new[]{Enemy("enemy",-3,0)});
        var snapshot=Snapshot(BattlefieldSpatialInput.Empty,participants);
        var result=_policy.Allocate(snapshot,new[]{
            Candidate("friendly",0,0),Candidate("outside",30,0),Candidate("dense",-3,0),Candidate("free",3,0),
            Candidate("free",3,0)},1,1,out var decisions);
        Assert.That(result[0].SpawnPointId,Is.EqualTo("free"));
        Assert.That(decisions,Has.Count.EqualTo(5));
        Assert.That(System.Linq.Enumerable.Any(decisions,d=>d.Reason=="Duplicate ID"),Is.True);
        Assert.That(System.Linq.Enumerable.Any(decisions,d=>d.Reason=="Player occupied cell"),Is.True);
        Assert.That(System.Linq.Enumerable.Any(decisions,d=>d.Id=="dense"&&d.EnemyCount==1&&d.AssignedSets==0),Is.True);
        Assert.That(System.Linq.Enumerable.Any(decisions,d=>d.Id=="free"&&d.AssignedSets==1),Is.True);
    }

    [Test]
    public void Allocate_RejectsFriendlyOccupiedAndOutsideCells()
    {
        var turrets = new BattlefieldSpatialInput(new[] { Turret("turret", 3d, -3d) });
        var participants = new BattlefieldDynamicSpatialInput(
            new[] { Player("player", -3d, -3d) }, Array.Empty<EnemySpatialInput>());
        BattlefieldSpatialSnapshot snapshot = Snapshot(turrets, participants);

        var result = _policy.Allocate(snapshot, new[]
        {
            Candidate("player-cell", -3d, -3d),
            Candidate("turret-cell", 3d, -3d),
            Candidate("outside", 11d, 0d),
            Candidate("free", -3d, 3d)
        }, 3, 1);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].SpawnPointId, Is.EqualTo("free"));
        Assert.That(result[0].Sets, Is.EqualTo(3));
    }

    [Test]
    public void Allocate_UsesExistingEnemyDensityBeforeSpreadingNewSets()
    {
        var participants = new BattlefieldDynamicSpatialInput(
            Array.Empty<PlayerSpatialInput>(), new[] { Enemy("existing", -3d, 0d) });
        BattlefieldSpatialSnapshot snapshot = Snapshot(BattlefieldSpatialInput.Empty, participants);

        var result = _policy.Allocate(snapshot, new[]
        {
            Candidate("crowded", -3d, 0d), Candidate("open", 3d, 0d)
        }, 1, 1);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].SpawnPointId, Is.EqualTo("open"));
    }

    [Test]
    public void Allocate_UsesFrontlineDistanceWhenGridPressureTies()
    {
        var turrets = new BattlefieldSpatialInput(new[]
        {
            Turret("a", -2d, -2d), Turret("b", -2d, 2d), Turret("c", 0d, 0d)
        });
        BattlefieldSpatialSnapshot snapshot = Snapshot(turrets, BattlefieldDynamicSpatialInput.Empty);
        Assert.That(snapshot.FrontlineEdges, Is.Not.Empty);

        var result = _policy.Allocate(snapshot, new[]
        {
            Candidate("far", 6d, 0d), Candidate("near", -4d, 0d)
        }, 1, 1);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].SpawnPointId, Is.EqualTo("near"));
    }

    [Test]
    public void Allocate_RepeatedSmallRequestsServeAllEmptyCellsEvenAfterEnemiesMoveAway()
    {
        var snapshot = Snapshot(BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var candidates = new[] { Candidate("a", -7, 0), Candidate("b", -3, 0), Candidate("c", 3, 0), Candidate("d", 7, 0) };
        var counts = candidates.ToDictionary(c => c.Id, c => 0);
        for (int i = 0; i < 20; i++)
        {
            var plan = _policy.Allocate(snapshot, candidates, 2, 5);
            foreach (var allocation in plan) counts[allocation.SpawnPointId] += allocation.Sets * 5;
            Commit(snapshot, plan, 5);
        }
        Assert.That(counts.Values, Is.All.EqualTo(50));
    }

    [Test]
    public void Allocate_ClonesInSameCellDoNotMultiplyCellShare()
    {
        var snapshot = Snapshot(BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var candidates = new[] { Candidate("a", -7, 0), Candidate("b", -6.5, 0), Candidate("c", 7, 0) };
        var counts = candidates.ToDictionary(c => c.Id, c => 0);
        for (int i = 0; i < 12; i++)
        {
            var plan = _policy.Allocate(snapshot, candidates, 2, 5);
            Assert.That(plan.Single(p => p.SpawnPointId == "c").Sets, Is.EqualTo(1));
            foreach (var allocation in plan) counts[allocation.SpawnPointId] += allocation.Sets * 5;
            Commit(snapshot, plan, 5);
        }
        Assert.That(counts["a"] + counts["b"], Is.EqualTo(counts["c"]));
        Assert.That(counts["a"], Is.EqualTo(counts["b"]));
    }

    [Test]
    public void Allocate_PersistentDensityDefersButDoesNotPermanentlyStarveCell()
    {
        var input = new BattlefieldDynamicSpatialInput(Array.Empty<PlayerSpatialInput>(),
            Enumerable.Range(0, 10).Select(i => Enemy("enemy-" + i, -3, 0)));
        var snapshot = Snapshot(BattlefieldSpatialInput.Empty, input);
        var candidates = new[] { Candidate("crowded", -3, 0), Candidate("open", 3, 0) };
        var counts = candidates.ToDictionary(c => c.Id, c => 0);
        for (int i = 0; i < 30; i++)
        {
            var plan = _policy.Allocate(snapshot, candidates, 1, 1);
            counts[plan[0].SpawnPointId]++;
            Commit(snapshot, plan, 1);
        }
        Assert.That(counts["crowded"], Is.EqualTo(10));
        Assert.That(counts["open"], Is.EqualTo(20));
    }

    [Test]
    public void Allocate_PlanningAndFailedExecutionDoNotSpendService()
    {
        var snapshot = Snapshot(BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var candidates = new[] { Candidate("a", -3, 0), Candidate("b", 3, 0) };
        Assert.That(_policy.Allocate(snapshot, candidates, 1, 5)[0].SpawnPointId, Is.EqualTo("a"));
        _policy.RecordExecuted(snapshot, Array.Empty<SpawnDecisionSnapshot.Actual>());
        Assert.That(_policy.Allocate(snapshot, candidates, 1, 5)[0].SpawnPointId, Is.EqualTo("a"));
        _policy.RecordExecuted(snapshot, new[] { new SpawnDecisionSnapshot.Actual("a", "Slime", "1", new BattlefieldPoint(-3, 0)),
            new SpawnDecisionSnapshot.Actual("a", "Slime", "2", new BattlefieldPoint(-3, 0)) });
        _policy.Allocate(snapshot, candidates, 1, 5, out var observations);
        Assert.That(observations.Single(c => c.Id == "a").CellService, Is.EqualTo(2));
        Assert.That(observations.Single(c => c.Id == "a").AssignedSets, Is.Zero);
    }

    [Test]
    public void Allocate_MatchChangeResetsHistoryAndOldExecutionCannotContaminateIt()
    {
        var old = Snapshot(BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var fresh = BattlefieldSpatialSnapshot.CreateMatchSnapshot(old, new MatchId("new-match"), 1, old.Grid);
        var candidates = new[] { Candidate("a", -3, 0), Candidate("b", 3, 0) };
        Commit(old, _policy.Allocate(old, candidates, 1, 5), 5);
        Assert.That(_policy.Allocate(old, candidates, 1, 5)[0].SpawnPointId, Is.EqualTo("b"));
        Assert.That(_policy.Allocate(fresh, candidates, 1, 5)[0].SpawnPointId, Is.EqualTo("a"));
        Commit(old, new[] { new SpatialSpawnAllocation("a", 1) }, 5);
        Assert.That(_policy.Allocate(fresh, candidates, 1, 5)[0].SpawnPointId, Is.EqualTo("a"));
    }

    [Test]
    public void Allocate_NewCellStartsAtCurrentMinimumServiceNotHistoricalZero()
    {
        var snapshot = Snapshot(BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var candidates = new[] { Candidate("a", -7, 0), Candidate("b", 7, 0) };
        Commit(snapshot, _policy.Allocate(snapshot, candidates, 8, 5), 5);
        var expanded = new[] { candidates[0], candidates[1], Candidate("c", 3, 0) };
        var next = _policy.Allocate(snapshot, expanded, 3, 5, out var observations);
        Assert.That(next, Has.Count.EqualTo(3));
        Assert.That(next.Select(p => p.Sets), Is.All.EqualTo(1));
        Assert.That(observations.Single(c => c.Id == "c").CellService, Is.EqualTo(20));
    }

    [Test]
    public void Allocate_GridXYChangeResetsButDrawPlaneChangePreservesHistory()
    {
        var snapshot = Snapshot(BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var candidates = new[] { Candidate("a", -3, 0), Candidate("b", 3, 0) };
        Commit(snapshot, _policy.Allocate(snapshot, candidates, 1, 5), 5);
        var drawGrid = new BattlefieldGridBuilder().Build(new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 10, 20, 20, 10, 10),
            BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var draw = BattlefieldSpatialSnapshot.CreateMatchSnapshot(snapshot, snapshot.MatchId, 2, drawGrid);
        Assert.That(_policy.Allocate(draw, candidates, 1, 5)[0].SpawnPointId, Is.EqualTo("b"));
        var resizedGrid = new BattlefieldGridBuilder().Build(new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 10, 20, 20, 20, 10),
            BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var resized = BattlefieldSpatialSnapshot.CreateMatchSnapshot(snapshot, snapshot.MatchId, 3, resizedGrid);
        Assert.That(_policy.Allocate(resized, candidates, 1, 5)[0].SpawnPointId, Is.EqualTo("a"));
    }

    [Test]
    public void Allocate_InputOrderDoesNotChangeCellOrPointSelection()
    {
        var snapshot = Snapshot(BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var candidates = new[] { Candidate("a", -7, 0), Candidate("b", -6.5, 0), Candidate("c", 7, 0) };
        var other = new SpawnPointSpatialPolicy();
        for (int i = 0; i < 8; i++)
        {
            var plan = _policy.Allocate(snapshot, candidates, 2, 5);
            var reversed = other.Allocate(snapshot, candidates.Reverse().ToArray(), 2, 5);
            Assert.That(reversed.Select(p => p.SpawnPointId + ":" + p.Sets), Is.EqualTo(plan.Select(p => p.SpawnPointId + ":" + p.Sets)));
            Commit(snapshot, plan, 5);
            other.RecordExecuted(snapshot, Actuals(reversed, 5));
        }
    }

    private void Commit(BattlefieldSpatialSnapshot snapshot, IReadOnlyList<SpatialSpawnAllocation> plan, int enemiesPerSet)
        => _policy.RecordExecuted(snapshot, Actuals(plan, enemiesPerSet));

    private static IReadOnlyList<SpawnDecisionSnapshot.Actual> Actuals(IReadOnlyList<SpatialSpawnAllocation> plan, int enemiesPerSet)
        => plan.SelectMany(p => Enumerable.Range(0, p.Sets * enemiesPerSet).Select(i =>
            new SpawnDecisionSnapshot.Actual(p.SpawnPointId, "Slime", p.SpawnPointId + "-" + i, new BattlefieldPoint(0, 0)))).ToArray();

    // Unity batchmode에서도 새 정책만 직접 확인할 수 있는 진입점입니다.
    public static void RunValidation()
    {
        int count = 0;
        foreach (var method in typeof(SpawnPointSpatialPolicyTests).GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0))
        { method.Invoke(new SpawnPointSpatialPolicyTests(), null); count++; }
        UnityEngine.Debug.Log($"SpawnPointSpatialPolicy validation: {count} passed.");
    }

    private static BattlefieldSpatialSnapshot Snapshot(
        BattlefieldSpatialInput turrets,
        BattlefieldDynamicSpatialInput participants)
    {
        var grid = new BattlefieldGridBuilder().Build(
            new BattlefieldGridConfiguration(new BattlefieldPoint(0d, 0d), 0d, 20d, 20d, 10, 10),
            turrets, participants);
        return BattlefieldSpatialSnapshot.CreateMatchSnapshot(
            new BattlefieldTopologyBuilder().Build(turrets), new MatchId("spatial-policy-test"), 1, grid);
    }

    private static TurretSpatialInput Turret(string id, double x, double y) =>
        new TurretSpatialInput(new EntityId(id), new BattlefieldPoint(x, y));

    private static PlayerSpatialInput Player(string id, double x, double y) =>
        new PlayerSpatialInput(new EntityId(id), new BattlefieldPoint(x, y));

    private static EnemySpatialInput Enemy(string id, double x, double y) =>
        new EnemySpatialInput(new EntityId(id), new BattlefieldPoint(x, y));

    private static SpatialSpawnCandidate Candidate(string id, double x, double y) =>
        new SpatialSpawnCandidate(id, new BattlefieldPoint(x, y));
}

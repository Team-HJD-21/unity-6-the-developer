// Stage 1 SpawnPoint 정책이 실제 Grid·Frontline 입력에 따라 달라지는지 검증합니다.
// Editor 테스트는 생성 프리팹이나 NetworkManager 없이 순수 배분 결과를 검사합니다.

using System;
using NUnit.Framework;
using TeamHJD.Game.Domain;

public sealed class SpawnPointSpatialPolicyTests
{
    private readonly SpawnPointSpatialPolicy _policy = new SpawnPointSpatialPolicy();

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

    // Unity batchmode에서도 새 정책만 직접 확인할 수 있는 진입점입니다.
    public static void RunValidation()
    {
        var tests = new SpawnPointSpatialPolicyTests();
        tests.Allocate_RejectsFriendlyOccupiedAndOutsideCells();
        tests.Allocate_UsesExistingEnemyDensityBeforeSpreadingNewSets();
        tests.Allocate_UsesFrontlineDistanceWhenGridPressureTies();
        UnityEngine.Debug.Log("SpawnPointSpatialPolicy validation: 3 passed.");
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

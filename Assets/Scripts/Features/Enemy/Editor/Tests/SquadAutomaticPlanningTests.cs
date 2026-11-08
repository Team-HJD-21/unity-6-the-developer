using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

/// <summary>
/// 상황별 편성 선택과 공유 예산·수량·셀 수용량 제약을 검사한다.
/// </summary>
public sealed class SquadAutomaticPlanningTests
{
    private readonly List<UnityEngine.Object> _objects = new();
    private EnemyCatalog _enemies;

    [SetUp]
    public void SetUp()
    {
        var definition = Create<EnemyDefinition>();
        var data = new SerializedObject(definition);
        data.FindProperty("_enemyId").stringValue = "unit";
        data.FindProperty("_spawnCost").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        _enemies = Create<EnemyCatalog>();
        SetReferences(_enemies, "enemyDefinitions", definition);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (UnityEngine.Object item in _objects) UnityEngine.Object.DestroyImmediate(item);
        _objects.Clear();
    }

    [Test]
    public void AutomaticPlan_SharesRequestBudgetAndRegionalBudgetAcrossPoints()
    {
        var planner = Planner(Preset("three", 3));
        var points = new[] { Point("a", -7, 0), Point("b", -6.5, 0), Point("c", 7, 0) };
        var request = new AreaSpawnPlanRequest(10, 500, new[]
        {
            new SpawnBudgetAllocation("west", 4, new[] { "a", "b" }),
            new SpawnBudgetAllocation("east", 9, new[] { "c" })
        });
        Assert.That(PlanAtPoints(planner, request, points, Snapshot(), new SquadSelectionSettings(),
            new FixedRandom(), out var plans), Is.True);
        Assert.That(plans.Sum(Count), Is.EqualTo(9));
        Assert.That(plans.Where(p => p.SpawnPointId != "c").Sum(Count), Is.EqualTo(3));
        Assert.That(plans.Where(p => p.SpawnPointId == "c").Sum(Count), Is.EqualTo(6));
        Assert.That(plans.All(p => Count(p) == 3), Is.True);
    }

    [Test]
    public void AutomaticPlan_CountLimitDoesNotProducePartialSquads()
    {
        var planner = Planner(Preset("three", 3));
        Assert.That(PlanAtPoints(planner, new AreaSpawnPlanRequest(100, 5),
            new[] { Point("a", -3, 0), Point("b", 3, 0) }, Snapshot(), new SquadSelectionSettings(),
            new FixedRandom(), out var plans), Is.True);
        Assert.That(plans.Sum(Count), Is.EqualTo(3));
    }

    [Test]
    public void AutomaticPlan_ClonedPointsShareCellCapacityWithDifferentSquadSizes()
    {
        var planner = Planner(Preset("two", 2), Preset("three", 3));
        var settings = new SquadSelectionSettings { maxEnemiesPerCell = 5 };
        Assert.That(PlanAtPoints(planner, new AreaSpawnPlanRequest(100, 500),
            new[] { Point("a", -7, 0), Point("b", -6.5, 0) }, Snapshot(), settings,
            new FixedRandom(), out var plans), Is.True);
        int count = plans.Sum(Count);
        Assert.That(count, Is.InRange(3, 5));
        Assert.That(planner.LastCandidates.All(c => c.CellPendingEnemies == count), Is.True);
        Assert.That(plans.All(p => Count(p) == 2 || Count(p) == 3), Is.True);
    }

    [Test]
    public void AutomaticPlan_ExhaustedRegionDoesNotBlockAnotherRegion()
    {
        var planner = Planner(Preset("three", 3));
        var request = new AreaSpawnPlanRequest(100, 500, new[]
        {
            new SpawnBudgetAllocation("empty", 1, new[] { "a" }),
            new SpawnBudgetAllocation("funded", 6, new[] { "b" })
        });
        Assert.That(PlanAtPoints(planner, request, new[] { Point("a", -3, 0), Point("b", 3, 0) },
            Snapshot(), new SquadSelectionSettings(), new FixedRandom(), out var plans), Is.True);
        Assert.That(plans.Sum(Count), Is.EqualTo(6));
        Assert.That(plans.All(p => p.SpawnPointId == "b"), Is.True);
    }

    [Test]
    public void AutomaticPlan_DifferentLocalSituationsSelectDifferentPresets()
    {
        var planner = Planner(Preset("elite", 2, EnemySquadRole.Elite), Preset("patrol", 2));
        var points = new[] { Point("left", -3, 0), Point("right", 7, 0) };
        var positions = points.ToDictionary(point => point.Id, point => point.Position);
        var attacks = new Dictionary<string, TargetAttackType>
        {
            ["left"] = TargetAttackType.Area,
            ["right"] = TargetAttackType.None
        };
        Assert.That(planner.TryPlan(new AreaSpawnPlanRequest(4, 4), points, Snapshot(), positions,
            attacks, 0, Array.Empty<BattlefieldPoint>(), new SquadSelectionSettings(),
            new FixedRandom(), out var plans), Is.True);
        Assert.That(plans.Single(p => p.SpawnPointId == "left").SquadOrder, Is.EqualTo("elite"));
        Assert.That(plans.Single(p => p.SpawnPointId == "right").SquadOrder, Is.EqualTo("patrol"));
    }

    [Test]
    public void AutomaticPlan_SameTargetCellSharesBudgetRegardlessOfSpawnPointCount()
    {
        var preset = Preset("three", 3);
        var settings = new SquadSelectionSettings { nearbyCellRadius = 0, maxEnemiesPerCell = 50 };
        int[] Run(bool addPoint)
        {
            var planner = Planner(preset);
            var points = addPoint
                ? new[] { Point("a", -7, 0), Point("a-copy", -6.5, 0), Point("b", 7, 0) }
                : new[] { Point("a", -7, 0), Point("b", 7, 0) };
            var targets = points.ToDictionary(p => p.Id, p => new BattlefieldPoint(p.Id == "b" ? 3 : -3, 0));
            var attacks = points.ToDictionary(p => p.Id, p => TargetAttackType.None);
            Assert.That(planner.TryPlan(new AreaSpawnPlanRequest(30, 500), points, Snapshot(), targets,
                attacks, 0, new[] { new BattlefieldPoint(-3, 0) }, settings, new FixedRandom(), out var plans), Is.True);
            foreach (SpawnBudgetAllocation allocation in planner.LastBudgetAllocations)
                Assert.That(plans.Where(p => allocation.SpawnPointIds.Contains(p.SpawnPointId)).Sum(Count),
                    Is.LessThanOrEqualTo(allocation.Budget));
            Assert.That(plans.Sum(Count), Is.LessThanOrEqualTo(30));
            Assert.That(planner.LastBudgetAllocations.Count, Is.EqualTo(2));
            return planner.LastBudgetAllocations.OrderBy(a => a.Id).Select(a => a.Budget).ToArray();
        }
        Assert.That(Run(true), Is.EqualTo(Run(false)));
    }

    [Test]
    public void Session_BudgetAreasExcludeOccupiedAndFullCellsAndRespectCheapestFittingPreset()
    {
        var source = new BattlefieldDynamicSpatialInput(
            new[] { new PlayerSpatialInput(new EntityId("blocking-player"), new BattlefieldPoint(-7, 0)) },
            new[] { new EnemySpatialInput(new EntityId("existing"), new BattlefieldPoint(3, 0)) });
        var points = new[] { Point("blocked", -7, 0), Point("full", 3, 0), Point("free", 7, 0) };
        var session = new SpawnPointSpatialPolicy().BeginPlanning(Snapshot(null, source), points,
            new SquadSelectionSettings { maxEnemiesPerCell = 1 });
        var targets = points.ToDictionary(p => p.Id, p => new BattlefieldPoint(0, 0));
        var areas = session.GetBudgetAreas(targets, 1, Array.Empty<BattlefieldPoint>(), capacity => capacity >= 1 ? 6 : 0);
        Assert.That(areas.Count, Is.EqualTo(1));
        Assert.That(areas[0].SpawnPointIds, Is.EqualTo(new[] { "free" }));
        Assert.That(areas[0].MinimumCost, Is.EqualTo(6));
    }

    [Test]
    public void Session_TargetCountsExcludeSourceReservations()
    {
        var dynamicInput = new BattlefieldDynamicSpatialInput(Array.Empty<PlayerSpatialInput>(),
            new[] { new EnemySpatialInput(new EntityId("existing"), new BattlefieldPoint(-1, 0)) });
        var a = Point("a", -3, 0);
        var b = Point("b", -1, 0);
        var session = new SpawnPointSpatialPolicy().BeginPlanning(Snapshot(null, dynamicInput),
            new[] { a, b }, new SquadSelectionSettings());
        session.Reserve(a, 3);
        Assert.That(session.GetAreaContext(b, b.Position, 1, Array.Empty<BattlefieldPoint>()).CellEnemyCount, Is.EqualTo(1));
        Assert.That(session.GetAreaContext(b, b.Position, 1, Array.Empty<BattlefieldPoint>()).NearbyEnemyCount, Is.EqualTo(1));
        session.Reserve(b, 2);
        Assert.That(session.GetAreaContext(a, b.Position, 1, Array.Empty<BattlefieldPoint>()).CellEnemyCount, Is.EqualTo(3));
        Assert.That(session.GetAreaContext(a, b.Position, 1, Array.Empty<BattlefieldPoint>()).NearbyEnemyCount, Is.EqualTo(1));
    }

    [Test]
    public void Session_WithoutFrontlineKeepsBalancedBaseWeight()
    {
        var point = Point("a", 0, 0);
        var settings = new SquadSelectionSettings();
        var session = new SpawnPointSpatialPolicy().BeginPlanning(Snapshot(), new[] { point }, settings);
        var area = session.GetAreaContext(point, point.Position, 0, Array.Empty<BattlefieldPoint>());
        var preset = Preset("balanced", 2, EnemySquadRole.Balanced, 6);
        Assert.That(SquadSelectionPolicy.CalculateWeight(preset, area, settings), Is.EqualTo(6d));
    }

    [Test]
    public void AutomaticPlan_UnavailableAuthorityRandomDoesNotPlanOrConsumeRandom()
    {
        var random = new FixedRandom { IsAvailable = false };
        Assert.That(PlanAtPoints(Planner(Preset("two", 2)), new AreaSpawnPlanRequest(10, 10),
            new[] { Point("a", 0, 0) }, Snapshot(), new SquadSelectionSettings(), random, out var plans), Is.False);
        Assert.That(plans, Is.Empty);
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void AutomaticPlan_UnfundedPointsAndZeroWeightPresetsAreExcluded()
    {
        var request = new AreaSpawnPlanRequest(20, 20,
            new[] { new SpawnBudgetAllocation("other", 20, new[] { "not-in-map" }) });
        var random = new FixedRandom();
        var planner = Planner(Preset("two", 2));
        Assert.That(PlanAtPoints(planner, request, new[] { Point("a", 0, 0) }, Snapshot(),
            new SquadSelectionSettings(), random, out _), Is.False);
        planner = Planner(Preset("disabled", 2, EnemySquadRole.Patrol, 0));
        Assert.That(PlanAtPoints(planner, new AreaSpawnPlanRequest(20, 20), new[] { Point("a", 0, 0) },
            Snapshot(), new SquadSelectionSettings(), random, out _), Is.False);
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void BudgetAllocations_RejectDoubleFundingTheSamePoint()
    {
        Assert.Throws<ArgumentException>(() => new AreaSpawnPlanRequest(20, 20, new[]
        {
            new SpawnBudgetAllocation("one", 10, new[] { "a" }),
            new SpawnBudgetAllocation("two", 10, new[] { "a" })
        }));
    }

    [Test]
    public void Plan_TargetCrowdingDoesNotBlockTheSelectedSpawnPoint()
    {
        var point = Point("a", -7, 0);
        var target = new BattlefieldPoint(7, 0);
        var enemies = Enumerable.Range(0, 30)
            .Select(i => new EnemySpatialInput(new EntityId("crowded-" + i), target)).ToArray();
        var snapshot = Snapshot(null,
            new BattlefieldDynamicSpatialInput(Array.Empty<PlayerSpatialInput>(), enemies));
        var positions = new Dictionary<string, BattlefieldPoint> { [point.Id] = target };
        var planner = Planner(Preset("two", 2));

        Assert.That(planner.TryPlan(new AreaSpawnPlanRequest(4, 4), new[] { point }, snapshot,
            positions, new Dictionary<string, TargetAttackType>(), 30, Array.Empty<BattlefieldPoint>(), new SquadSelectionSettings(), new FixedRandom(), out var plans), Is.True);
        Assert.That(plans.Sum(Count), Is.EqualTo(4));
        var session = new SpawnPointSpatialPolicy().BeginPlanning(snapshot, new[] { point }, new SquadSelectionSettings());
        Assert.That(session.GetAreaContext(point, target, 30, Array.Empty<BattlefieldPoint>()).NearbyEnemyCount, Is.EqualTo(30));
        Assert.That(session.GetAreaContext(point, target, 30, Array.Empty<BattlefieldPoint>()).CellEnemyCount, Is.Zero);
    }

    [Test]
    public void TargetQuery_TreatsCuAsTurretAndExcludesPlayersAndUnavailableTurrets()
    {
        var player = Target("player", new Vector3(0.1f, 0, 0), TargetType.Player);
        var unavailable = Target("inactive turret", new Vector3(0.2f, 0, 0), TargetType.Turret);
        unavailable.enabled = false;
        var cu = Target("CU", new Vector3(1, 0, 100), TargetType.Turret);
        var fartherTurret = Target("turret", new Vector3(2, 0, 0), TargetType.Turret);
        var candidates = new[] { player, unavailable, fartherTurret, cu };

        Assert.That(EnemyTargetQuery.TryFindNearest(Vector3.zero, candidates, TargetType.Turret, out var selected), Is.True);
        Assert.That(selected, Is.SameAs(cu));
        cu.SetHealth(0, 10);
        Assert.That(EnemyTargetQuery.TryFindNearest(Vector3.zero, candidates, TargetType.Turret, out selected), Is.True);
        Assert.That(selected, Is.SameAs(fartherTurret));
        fartherTurret.SetHealth(0, 10);
        Assert.That(EnemyTargetQuery.TryFindNearest(Vector3.zero, candidates, TargetType.Turret, out selected), Is.False);
        Assert.That(selected, Is.Null);
    }

    [Test]
    public void Plan_MissingTargetPositionDoesNotConsumeRandom()
    {
        var random = new FixedRandom();
        var planner = Planner(Preset("two", 2));
        Assert.That(planner.TryPlan(new AreaSpawnPlanRequest(10, 10), new[] { Point("a", 0, 0) },
            Snapshot(), new Dictionary<string, BattlefieldPoint>(), new Dictionary<string, TargetAttackType>(), 0, Array.Empty<BattlefieldPoint>(), new SquadSelectionSettings(), random, out var plans), Is.False);
        Assert.That(plans, Is.Empty);
        Assert.That(random.Calls, Is.Zero);
    }

    [TestCase(200, 1000, 20f, true)]
    [TestCase(201, 1000, 20f, false)]
    [TestCase(300, 1000, 30f, true)]
    [TestCase(301, 1000, 30f, false)]
    [TestCase(0, 0, 20f, true)]
    [TestCase(0, 1000, 0f, true)]
    [TestCase(1, 1000, 0f, false)]
    [TestCase(1000, 1000, 100f, true)]
    public void PercentageThreshold_SelectsSparseOrCrowdedWeight(
        int nearby, int total, float threshold, bool expectedSparse)
    {
        var settings = new SquadSelectionSettings { sparseEnemyPercentage = threshold };
        var area = new SpawnAreaContext(0, nearby, total, false, TargetAttackType.None);
        var patrol = Preset("patrol", 2, EnemySquadRole.Patrol);
        var sustain = Preset("sustain", 2, EnemySquadRole.Sustain);

        Assert.That(SquadSelectionPolicy.CalculateWeight(patrol, area, settings),
            Is.EqualTo(expectedSparse ? 3d : 1d));
        Assert.That(SquadSelectionPolicy.CalculateWeight(sustain, area, settings),
            Is.EqualTo(expectedSparse ? 1d : 3d));
        Assert.That(area.NearbyEnemyPercentage,
            Is.EqualTo(total == 0 ? 0d : (double)nearby / total * 100d));
    }

    [TestCase(-1f)]
    [TestCase(101f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void PercentageThreshold_RejectsInvalidSettings(float threshold)
    {
        Assert.That(new SquadSelectionSettings { sparseEnemyPercentage = threshold }.IsValid, Is.False);
    }

    [TestCase(0, 0, true)]
    [TestCase(-2, 0, true)]
    [TestCase(3.9, 3.9, true)]
    [TestCase(4, 0, false)]
    [TestCase(-2.1, 0, false)]
    [TestCase(100, 0, false)]
    public void Session_PlayerPresenceUsesTheTargetNeighbourhood(double x, double y, bool expected)
    {
        var point = Point("spawn", -7, 0);
        var session = new SpawnPointSpatialPolicy().BeginPlanning(Snapshot(), new[] { point }, new SquadSelectionSettings());
        var area = session.GetAreaContext(point, new BattlefieldPoint(0, 0), 0,
            new[] { new BattlefieldPoint(x, y) });
        Assert.That(area.HasNearbyPlayer, Is.EqualTo(expected));
    }

    [Test]
    public void Session_OneNearbyPlayerIsEnoughAndTheTargetAreaIsCached()
    {
        var a = Point("a", -7, 0);
        var b = Point("b", -6.5, 0);
        var settings = new SquadSelectionSettings();
        var snapshot = Snapshot();
        var session = new SpawnPointSpatialPolicy().BeginPlanning(snapshot, new[] { a, b }, settings);
        var players = new CountingPlayerPositions(new BattlefieldPoint(8, 0), new BattlefieldPoint(1, 0), new BattlefieldPoint(9, 0));
        var target = new BattlefieldPoint(0, 0);
        Assert.That(session.GetAreaContext(a, target, 0, players).HasNearbyPlayer, Is.True);
        Assert.That(players.Visits, Is.EqualTo(2));
        Assert.That(session.GetAreaContext(b, target, 0, players).HasNearbyPlayer, Is.True);
        Assert.That(players.Visits, Is.EqualTo(2));
    }

    [Test]
    public void Session_RawGridPlayerPresenceDoesNotReplaceValidPlayerPositions()
    {
        var raw = new BattlefieldDynamicSpatialInput(
            new[] { new PlayerSpatialInput(new EntityId("unavailable-player"), new BattlefieldPoint(0, 0)) },
            Array.Empty<EnemySpatialInput>());
        var point = Point("spawn", -7, 0);
        var snapshot = Snapshot(null, raw);
        var session = new SpawnPointSpatialPolicy().BeginPlanning(snapshot, new[] { point }, new SquadSelectionSettings());
        var area = session.GetAreaContext(point, new BattlefieldPoint(0, 0), 0, Array.Empty<BattlefieldPoint>());
        Assert.That(area.HasNearbyPlayer, Is.False);
    }

    [TestCase(0, 1d)]
    [TestCase(1, 3d)]
    [TestCase(3, 3d)]
    public void Plan_PlayerPresenceBoostsAmbushOnce(int playerCount, double expectedWeight)
    {
        var point = Point("spawn", -7, 0);
        var snapshot = Snapshot();
        var settings = new SquadSelectionSettings();
        var players = Enumerable.Repeat(new BattlefieldPoint(0, 0), playerCount).ToArray();
        var session = new SpawnPointSpatialPolicy().BeginPlanning(snapshot, new[] { point }, settings);
        var area = session.GetAreaContext(point, new BattlefieldPoint(0, 0), 0, players);
        var ambush = Preset("ambush", 2, EnemySquadRole.Ambush);
        Assert.That(SquadSelectionPolicy.CalculateWeight(ambush, area, settings), Is.EqualTo(expectedWeight));
        Assert.That(Planner(ambush).TryPlan(new AreaSpawnPlanRequest(2, 2), new[] { point }, snapshot,
            new Dictionary<string, BattlefieldPoint> { [point.Id] = new BattlefieldPoint(0, 0) },
            new Dictionary<string, TargetAttackType>(), 0, players, settings, new FixedRandom(), out var plans), Is.True);
        Assert.That(plans.Sum(Count), Is.EqualTo(2));
    }

    private sealed class CountingPlayerPositions : IReadOnlyList<BattlefieldPoint>
    {
        private readonly BattlefieldPoint[] _positions;
        public int Visits { get; private set; }
        public int Count => _positions.Length;
        public BattlefieldPoint this[int index] => _positions[index];
        public CountingPlayerPositions(params BattlefieldPoint[] positions) => _positions = positions;
        public IEnumerator<BattlefieldPoint> GetEnumerator()
        {
            foreach (var position in _positions)
            {
                Visits++;
                yield return position;
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [TestCase(typeof(FieldCanonTurretLv1), TargetAttackType.SingleTarget)]
    [TestCase(typeof(FieldMissileTurretLV1), TargetAttackType.Area)]
    [TestCase(typeof(ControlUnitHealth), TargetAttackType.None)]
    [TestCase(typeof(FieldLaserTurret), TargetAttackType.Unknown)]
    public void Adapter_ClassifiesTheConnectedSource(Type sourceType, TargetAttackType expected)
    {
        var go = new GameObject("classification source");
        go.SetActive(false);
        _objects.Add(go);
        var source = go.AddComponent(sourceType);
        var adapter = go.AddComponent<DamageableTargetAdapter>();
        var data = new SerializedObject(adapter);
        data.FindProperty("_damageableSource").objectReferenceValue = source;
        data.ApplyModifiedPropertiesWithoutUndo();
        typeof(DamageableTargetAdapter).GetMethod("ResolveDamageable",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(adapter, null);

        Assert.That(adapter.AttackType, Is.EqualTo(expected));
        UnityEngine.Object.DestroyImmediate(source);
        Assert.That(adapter.AttackType, Is.EqualTo(TargetAttackType.Unknown));
    }

    [Test]
    public void Adapter_MissingSourceIsUnknownRatherThanAnUnarmedTarget()
    {
        var go = new GameObject("missing source");
        go.SetActive(false);
        _objects.Add(go);
        Assert.That(go.AddComponent<DamageableTargetAdapter>().AttackType, Is.EqualTo(TargetAttackType.Unknown));
    }

    [TestCase(EnemySquadRole.Patrol, TargetAttackType.None, 200, false, 3d)]
    [TestCase(EnemySquadRole.Patrol, TargetAttackType.None, 200, true, 1d)]
    [TestCase(EnemySquadRole.Patrol, TargetAttackType.Unknown, 200, false, 1d)]
    [TestCase(EnemySquadRole.Assault, TargetAttackType.SingleTarget, 200, false, 3d)]
    [TestCase(EnemySquadRole.Assault, TargetAttackType.SingleTarget, 201, false, 1d)]
    [TestCase(EnemySquadRole.Assault, TargetAttackType.Area, 200, false, 1d)]
    [TestCase(EnemySquadRole.Backline, TargetAttackType.Area, 200, false, 3d)]
    [TestCase(EnemySquadRole.Backline, TargetAttackType.SingleTarget, 200, false, 1d)]
    [TestCase(EnemySquadRole.Summon, TargetAttackType.SingleTarget, 200, false, 3d)]
    [TestCase(EnemySquadRole.Summon, TargetAttackType.Area, 200, false, 1d)]
    [TestCase(EnemySquadRole.Sustain, TargetAttackType.Unknown, 201, false, 3d)]
    [TestCase(EnemySquadRole.Sustain, TargetAttackType.Unknown, 200, false, 1d)]
    [TestCase(EnemySquadRole.Elite, TargetAttackType.Area, 200, false, 3d)]
    [TestCase(EnemySquadRole.Elite, TargetAttackType.Area, 201, false, 1d)]
    [TestCase(EnemySquadRole.Balanced, TargetAttackType.Area, 200, false, 1d)]
    [TestCase(EnemySquadRole.Ambush, TargetAttackType.Unknown, 200, true, 3d)]
    public void Policy_UsesAttackPatternPopulationAndPlayer(
        EnemySquadRole role, TargetAttackType attackType, int nearby, bool player, double expectedWeight)
    {
        var preset = Preset("rule", 2, role);
        var settings = new SquadSelectionSettings();
        var area = new SpawnAreaContext(0, nearby, 1000, player, attackType);
        Assert.That(SquadSelectionPolicy.CalculateWeight(preset, area, settings), Is.EqualTo(expectedWeight));
    }

    [Test]
    public void Session_SharedTargetCellKeepsEachTargetsAttackType()
    {
        var point = Point("spawn", -7, 0);
        var session = new SpawnPointSpatialPolicy().BeginPlanning(Snapshot(), new[] { point }, new SquadSelectionSettings());
        var players = Array.Empty<BattlefieldPoint>();
        var first = session.GetAreaContext(point, new BattlefieldPoint(0, 0), 0, players, TargetAttackType.SingleTarget);
        var second = session.GetAreaContext(point, new BattlefieldPoint(1, 0), 0, players, TargetAttackType.Area);
        Assert.That(first.AttackType, Is.EqualTo(TargetAttackType.SingleTarget));
        Assert.That(second.AttackType, Is.EqualTo(TargetAttackType.Area));
    }

    private TargetableComponent Target(string name, Vector3 position, TargetType type)
    {
        var go = new GameObject(name);
        _objects.Add(go);
        go.transform.position = position;
        var target = go.AddComponent<TargetableComponent>();
        target.SetTargetType(type);
        target.SetHealth(10, 10);
        return target;
    }

    private static bool PlanAtPoints(SpawnCompositionPlanner planner, AreaSpawnPlanRequest request,
        IReadOnlyList<SpatialSpawnCandidate> points, BattlefieldSpatialSnapshot snapshot,
        SquadSelectionSettings settings, IRandomSource random, out IReadOnlyList<SpawnInstruction> plans)
    {
        var positions = points.ToDictionary(point => point.Id, point => point.Position);
        int totalEnemyCount = snapshot.Grid.Cells.Sum(cell => cell.EnemyCount) + snapshot.Grid.OutOfBoundsEnemyIds.Count;
        var attacks = points.ToDictionary(point => point.Id, point => TargetAttackType.None);
        return planner.TryPlan(request, points, snapshot, positions, attacks, totalEnemyCount, Array.Empty<BattlefieldPoint>(), settings, random, out plans);
    }

    private EnemySquadPreset Preset(string id, int count, EnemySquadRole role = EnemySquadRole.Patrol, int weight = 1)
    {
        var preset = Create<EnemySquadPreset>();
        var data = new SerializedObject(preset);
        data.FindProperty("_squadOrder").stringValue = id;
        data.FindProperty("role").intValue = (int)role;
        data.FindProperty("selectionWeight").intValue = weight;
        var entries = data.FindProperty("_composition");
        entries.arraySize = 1;
        entries.GetArrayElementAtIndex(0).FindPropertyRelative("_enemyId").stringValue = "unit";
        entries.GetArrayElementAtIndex(0).FindPropertyRelative("_weight").intValue = count;
        data.ApplyModifiedPropertiesWithoutUndo();
        return preset;
    }

    private SpawnCompositionPlanner Planner(params EnemySquadPreset[] presets)
    {
        var catalog = Create<EnemySquadPresetCatalog>();
        SetReferences(catalog, "squadPresets", presets);
        return new SpawnCompositionPlanner(catalog, _enemies);
    }

    private T Create<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        _objects.Add(value);
        return value;
    }

    private static void SetReferences(UnityEngine.Object target, string field, params UnityEngine.Object[] values)
    {
        var data = new SerializedObject(target);
        var list = data.FindProperty(field);
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int Count(SpawnInstruction plan) => plan.Enemies.Sum(e => e.Count);
    private static SpatialSpawnCandidate Point(string id, double x, double y) => new(id, new BattlefieldPoint(x, y));
    private static TurretSpatialInput Turret(string id, double x, double y) => new(new EntityId(id), new BattlefieldPoint(x, y));
    private static BattlefieldSpatialSnapshot Snapshot(BattlefieldSpatialInput turrets = null,
        BattlefieldDynamicSpatialInput participants = null)
    {
        turrets ??= BattlefieldSpatialInput.Empty;
        participants ??= BattlefieldDynamicSpatialInput.Empty;
        var grid = new BattlefieldGridBuilder().Build(
            new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 20, 20, 10, 10), turrets, participants);
        return BattlefieldSpatialSnapshot.CreateMatchSnapshot(new BattlefieldTopologyBuilder().Build(turrets),
            new MatchId("automatic-planning-test"), 1, grid);
    }

    private sealed class FixedRandom : IRandomSource
    {
        public bool IsAvailable { get; set; } = true;
        public int Calls { get; private set; }
        public double NextDouble(double minInclusive, double maxExclusive)
        {
            Calls++;
            return minInclusive + (maxExclusive - minInclusive) * 0.5d;
        }
        public double NextDouble() => NextDouble(0d, 1d);
        public uint NextUInt32() => throw new NotSupportedException();
        public int NextInt(int minInclusive, int maxExclusive) => throw new NotSupportedException();
        public float NextFloat() => throw new NotSupportedException();
        public float NextFloat(float minInclusive, float maxExclusive) => throw new NotSupportedException();
    }
}

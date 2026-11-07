using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

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
        var request = new AutomaticSpawnRequest(10, 500, new[]
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
        Assert.That(PlanAtPoints(planner, new AutomaticSpawnRequest(100, 5),
            new[] { Point("a", -3, 0), Point("b", 3, 0) }, Snapshot(), new SquadSelectionSettings(),
            new FixedRandom(), out var plans), Is.True);
        Assert.That(plans.Sum(Count), Is.EqualTo(3));
    }

    [Test]
    public void AutomaticPlan_ClonedPointsShareCellCapacityWithDifferentSquadSizes()
    {
        var planner = Planner(Preset("two", 2), Preset("three", 3));
        var settings = new SquadSelectionSettings { maxEnemiesPerCell = 5 };
        Assert.That(PlanAtPoints(planner, new AutomaticSpawnRequest(100, 500),
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
        var request = new AutomaticSpawnRequest(100, 500, new[]
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
        var turrets = new BattlefieldSpatialInput(new[]
        {
            Turret("t1", -4.5, -1), Turret("t2", -4.5, 1), Turret("t3", -1, 1)
        });
        var settings = new SquadSelectionSettings { nearFrontlineDistance = 3, approachFrontlineDistance = 8 };
        Assert.That(PlanAtPoints(planner, new AutomaticSpawnRequest(4, 4),
            new[] { Point("left", -3, 0), Point("right", 7, 0) }, Snapshot(turrets), settings,
            new FixedRandom(), out var plans), Is.True);
        Assert.That(plans.Single(p => p.SpawnPointId == "left").SquadOrder, Is.EqualTo("elite"));
        Assert.That(plans.Single(p => p.SpawnPointId == "right").SquadOrder, Is.EqualTo("patrol"));
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
        Assert.That(session.GetAreaContext(b, b.Position).CellEnemyCount, Is.EqualTo(1));
        Assert.That(session.GetAreaContext(b, b.Position).NearbyEnemyCount, Is.EqualTo(1));
        session.Reserve(b, 2);
        Assert.That(session.GetAreaContext(a, b.Position).CellEnemyCount, Is.EqualTo(3));
        Assert.That(session.GetAreaContext(a, b.Position).NearbyEnemyCount, Is.EqualTo(1));
    }

    [Test]
    public void Session_MissingFrontlineDoesNotApplyNearFrontlineBonus()
    {
        var point = Point("a", 0, 0);
        var settings = new SquadSelectionSettings();
        var session = new SpawnPointSpatialPolicy().BeginPlanning(Snapshot(), new[] { point }, settings);
        var area = session.GetAreaContext(point, point.Position);
        Assert.That(area.HasFrontline, Is.False);
        var preset = Preset("balanced", 2, EnemySquadRole.Balanced, 6);
        Assert.That(SquadSelectionPolicy.CalculateWeight(preset, area, settings), Is.EqualTo(6d));
    }

    [Test]
    public void AutomaticPlan_UnavailableAuthorityRandomDoesNotPlanOrConsumeRandom()
    {
        var random = new FixedRandom { IsAvailable = false };
        Assert.That(PlanAtPoints(Planner(Preset("two", 2)), new AutomaticSpawnRequest(10, 10),
            new[] { Point("a", 0, 0) }, Snapshot(), new SquadSelectionSettings(), random, out var plans), Is.False);
        Assert.That(plans, Is.Empty);
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void AutomaticPlan_UnfundedPointsAndZeroWeightPresetsAreExcluded()
    {
        var request = new AutomaticSpawnRequest(20, 20,
            new[] { new SpawnBudgetAllocation("other", 20, new[] { "not-in-map" }) });
        var random = new FixedRandom();
        var planner = Planner(Preset("two", 2));
        Assert.That(PlanAtPoints(planner, request, new[] { Point("a", 0, 0) }, Snapshot(),
            new SquadSelectionSettings(), random, out _), Is.False);
        planner = Planner(Preset("disabled", 2, EnemySquadRole.Patrol, 0));
        Assert.That(PlanAtPoints(planner, new AutomaticSpawnRequest(20, 20), new[] { Point("a", 0, 0) },
            Snapshot(), new SquadSelectionSettings(), random, out _), Is.False);
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void BudgetAllocations_RejectDoubleFundingTheSamePoint()
    {
        Assert.Throws<ArgumentException>(() => new AutomaticSpawnRequest(20, 20, new[]
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

        Assert.That(planner.TryPlan(new AutomaticSpawnRequest(4, 4), new[] { point }, snapshot,
            positions, new SquadSelectionSettings(), new FixedRandom(), out var plans), Is.True);
        Assert.That(plans.Sum(Count), Is.EqualTo(4));
        var session = new SpawnPointSpatialPolicy().BeginPlanning(snapshot, new[] { point }, new SquadSelectionSettings());
        Assert.That(session.GetAreaContext(point, target).NearbyEnemyCount, Is.EqualTo(30));
        Assert.That(session.GetAreaContext(point, target).CellEnemyCount, Is.Zero);
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
        Assert.That(planner.TryPlan(new AutomaticSpawnRequest(10, 10), new[] { Point("a", 0, 0) },
            Snapshot(), new Dictionary<string, BattlefieldPoint>(), new SquadSelectionSettings(), random, out var plans), Is.False);
        Assert.That(plans, Is.Empty);
        Assert.That(random.Calls, Is.Zero);
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

    private static bool PlanAtPoints(SpawnCompositionPlanner planner, AutomaticSpawnRequest request,
        IReadOnlyList<SpatialSpawnCandidate> points, BattlefieldSpatialSnapshot snapshot,
        SquadSelectionSettings settings, IRandomSource random, out IReadOnlyList<SpawnInstruction> plans)
    {
        var positions = points.ToDictionary(point => point.Id, point => point.Position);
        return planner.TryPlan(request, points, snapshot, positions, settings, random, out plans);
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

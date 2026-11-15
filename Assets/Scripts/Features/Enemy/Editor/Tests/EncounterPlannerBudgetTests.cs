using TeamHJD.Game.Enemies.Encounter;
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Runtime의 점유율 예산과 Static 증가 상태 및 입력 검증을 검사한다.
/// </summary>
public sealed class EncounterPlannerBudgetTests
{
    private readonly MatchId _match = new("runtime-budget-test");
    private GameObject _root;
    private EnemySpawnExecutor _executor;
    private EncounterBudgetSettings _settings;
    private EncounterPipeline _runtime;
    private BattlefieldSpatialSnapshot _battlefield;
    private EncounterExecutionContext _context;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Runtime budget test");
        _root.SetActive(false);
        _root.AddComponent<NetworkObject>();
        _executor = _root.AddComponent<EnemySpawnExecutor>();
        _settings = new EncounterBudgetSettings();
        _runtime = new EncounterPipeline(_match, _executor, null, _settings);
        _battlefield = Battlefield(_match);
        _context = new EncounterExecutionContext(_match, 1, _battlefield.Revision, EncounterRequestSource.RuntimeUI);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [TestCase(RegionPolicyMode.Static, 25, 30)]
    [TestCase(RegionPolicyMode.Dynamic, 60, 100)]
    public void Budget_UsesLatestSnapshotAndMode(RegionPolicyMode mode, int middle, int final)
    {
        AssertBudget(Region(0, mode), 20);
        AssertBudget(Region(2, mode), middle);
        AssertBudget(Region(4, mode), final);
    }

    [Test]
    public void StaticBudget_ApproachesLatestTargetClampsAndDropsImmediately()
    {
        AssertBudget(Region(1), 40);
        AssertBudget(Region(4), 45);
        AssertBudget(Region(4), 50);
        AssertBudget(Region(2), 55);
        AssertBudget(Region(2), 60);
        AssertBudget(Region(2), 60);
        AssertBudget(Region(0), 20);
    }

    [Test]
    public void StaticBudget_UsesUpdatedSettingsWithoutIntegerOverflow()
    {
        _settings.staticIncreasePerRequest = 7;
        AssertBudget(Region(0), 20);
        AssertBudget(Region(1), 27);
        AssertBudget(Region(1), 34);
        AssertBudget(Region(1), 40);
        _settings.staticIncreasePerRequest = int.MaxValue;
        AssertBudget(Region(4), 100);
    }

    [Test]
    public void RestoredStaticBudget_ContinuesItsPreviousStepInsteadOfJumpingToTheCurrentCaptureTarget()
    {
        AssertBudget(Region(0), 20);
        AssertBudget(Region(4), 25);
        object planner = typeof(EncounterPipeline).GetField("_planner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_runtime);
        var budgetProperty = typeof(EncounterPlanner).GetProperty("CapturedBudget", BindingFlags.Instance | BindingFlags.NonPublic);
        var restore = typeof(EncounterPlanner).GetMethod("RestoreBudget", BindingFlags.Instance | BindingFlags.NonPublic);
        int saved = (int)budgetProperty.GetValue(planner);
        AssertBudget(Region(4), 30);
        restore.Invoke(planner, new object[] { saved });
        AssertBudget(Region(4), 30);
    }

    [Test]
    public void RuntimeInstances_DoNotShareBudgetAndRequireMatchingInputs()
    {
        AssertBudget(Region(0), 20);
        AssertBudget(Region(4), 25);
        var second = new EncounterPipeline(_match, _executor, null, _settings);
        Assert.That(ReadBudget(second, _battlefield, Region(4), _context, out int fresh, out _), Is.True);
        Assert.That(fresh, Is.EqualTo(100));

        var other = new MatchId("other");
        Assert.That(ReadBudget(_runtime, _battlefield, Region(4, match: other), _context, out _, out _), Is.False);
        Assert.That(ReadBudget(_runtime, Battlefield(other), Region(4), _context, out _, out _), Is.False);
        var wrongContext = new EncounterExecutionContext(other, 1, 1, EncounterRequestSource.RuntimeUI);
        Assert.That(ReadBudget(_runtime, _battlefield, Region(4), wrongContext, out _, out _), Is.False);
        Assert.That(ReadBudget(_runtime, _battlefield, null, _context, out _, out _), Is.False);
        Assert.That(ReadBudget(_runtime, _battlefield, Region(4), null, out _, out _), Is.False);
        AssertBudget(Region(4), 30);
    }

    [TestCase(0, 100, 5)]
    [TestCase(20, 19, 5)]
    [TestCase(20, 100, 0)]
    public void InvalidSettings_DoNotAdvanceBudget(int minimum, int maximum, int increase)
    {
        AssertBudget(Region(0), 20);
        _settings.baseBudget = minimum;
        _settings.maximumBudget = maximum;
        _settings.staticIncreasePerRequest = increase;
        Assert.That(ReadBudget(_runtime, _battlefield, Region(4), _context, out int budget, out _), Is.False);
        Assert.That(budget, Is.Zero);
        _settings.baseBudget = 20;
        _settings.maximumBudget = 100;
        _settings.staticIncreasePerRequest = 5;
        AssertBudget(Region(4), 25);
    }

    [Test]
    public void PublicRequests_RejectNonServerWithoutAdvancingBudgetOrConsumingRandom()
    {
        var random = new UnexpectedRandom();
        Assert.That(_runtime.SpawnRequest(_battlefield, Region(4), 10, new SquadSelectionSettings(), random,
            _context, out _, out var automatic), Is.False);
        Assert.That(automatic.SpawnedCount, Is.Zero);
        Assert.That(_runtime.SpawnRequest(_battlefield, Region(4), "Normal", 10,
            _context, out _, out var fixedPreset), Is.False);
        Assert.That(fixedPreset.SpawnedCount, Is.Zero);
        AssertBudget(Region(0), 20);
    }

    [Test]
    public void Constructor_UsesExplicitExecutorAndRejectsIncompleteDependencies()
    {
        Assert.That(_runtime.Executor, Is.SameAs(_executor));
        Assert.Throws<ArgumentException>(() => new EncounterPipeline(default, _executor, null, _settings));
        Assert.Throws<ArgumentNullException>(() => new EncounterPipeline(_match, null, null, _settings));
        Assert.Throws<ArgumentNullException>(() => new EncounterPipeline(_match, _executor, null, null));
    }

    private void AssertBudget(RegionGridSnapshot region, int expected)
    {
        Assert.That(ReadBudget(_runtime, _battlefield, region, _context, out int budget, out string reason), Is.True, reason);
        Assert.That(budget, Is.EqualTo(expected));
    }

    private static bool ReadBudget(EncounterPipeline runtime, BattlefieldSpatialSnapshot battlefield,
        RegionGridSnapshot region, EncounterExecutionContext context, out int budget, out string reason)
    {
        object[] arguments = { battlefield, region, context, false, 0, null };
        var planner = (EncounterPlanner)typeof(EncounterPipeline).GetField("_planner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
        bool success = planner.TryDetermineBudget(battlefield, region, context, false, out int value, out string message);
        arguments[4] = value;
        arguments[5] = message;
        budget = (int)arguments[4];
        reason = (string)arguments[5];
        return success;
    }

    private RegionGridSnapshot Region(int captured, RegionPolicyMode mode = RegionPolicyMode.Static, MatchId match = default)
    {
        var geometry = new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 2, 2, 2, 2);
        return RegionGridSnapshot.FromCapturedCells(match.IsEmpty ? _match : match, Guid.NewGuid(), 1, mode,
            new RegionGridDefinition(geometry), Enumerable.Range(0, captured));
    }

    private static BattlefieldSpatialSnapshot Battlefield(MatchId match)
    {
        var geometry = new BattlefieldTopologyBuilder().Build(BattlefieldSpatialInput.Empty);
        var grid = new BattlefieldGridBuilder().Build(BattlefieldGridConfiguration.Default,
            BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        return BattlefieldSpatialSnapshot.CreateMatchSnapshot(geometry, match, 1, grid);
    }

    private sealed class UnexpectedRandom : IRandomSource
    {
        public bool IsAvailable => true;
        public uint NextUInt32() => throw new InvalidOperationException("Unexpected random consumption.");
        public int NextInt(int min, int max) => throw new InvalidOperationException("Unexpected random consumption.");
        public float NextFloat() => throw new InvalidOperationException("Unexpected random consumption.");
        public float NextFloat(float min, float max) => throw new InvalidOperationException("Unexpected random consumption.");
        public double NextDouble() => throw new InvalidOperationException("Unexpected random consumption.");
        public double NextDouble(double min, double max) => throw new InvalidOperationException("Unexpected random consumption.");
    }
}

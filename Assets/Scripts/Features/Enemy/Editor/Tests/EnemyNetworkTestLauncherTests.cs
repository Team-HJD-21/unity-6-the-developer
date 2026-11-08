using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Bootstrap.SceneComposition;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class EnemyNetworkTestLauncherTests
{
    private GameObject _root;
    private EnemyNetworkTestLauncher _launcher;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Launcher test");
        _root.SetActive(false);
        _launcher = _root.AddComponent<EnemyNetworkTestLauncher>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void RandomBinding_UsesNamedStreamAndCanRebindAfterRelease()
    {
        var streams = new TestStreams();
        IRandomSource source;
        using (((IRandomConsumer)_launcher).BindRandom(streams))
        {
            source = Get<IRandomSource>("_squadRandom");
            Assert.That(streams.LastStream, Is.EqualTo("enemy/squad-selection"));
            Assert.That(source.IsAvailable, Is.True);
        }
        Assert.That(source.IsAvailable, Is.False);
        Assert.Throws<ObjectDisposedException>(() => source.NextDouble());
        using (_launcher.BindRandom(streams))
            Assert.That(Get<IRandomSource>("_squadRandom").IsAvailable, Is.True);
    }

    [Test]
    public void RequestSpawn_WithoutHostDoesNotReadSnapshotOrConsumeRandom()
    {
        Set("_networkManager", _root.GetComponent<NetworkManager>());
        Set("_snapshotProvider", (Func<BattlefieldSpatialSnapshot>)(() =>
            throw new InvalidOperationException("A rejected request must not capture a snapshot.")));
        using (_launcher.BindRandom(new TestStreams()))
        {
            Assert.That(_launcher.RequestSpawn(out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(Get<long>("_requestSequence"), Is.Zero);
        }
    }

    [Test]
    public void FixedRequest_PreservesCoreSequenceWithoutReusingLauncherDiagnosticIds()
    {
        var match = new MatchId("launcher-test");
        Set("_boundMatchId", match);
        Set("_requestSequence", 10L);
        var geometry = new BattlefieldTopologyBuilder().Build(BattlefieldSpatialInput.Empty);
        var grid = new BattlefieldGridBuilder().Build(BattlefieldGridConfiguration.Default,
            BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
        var snapshot = BattlefieldSpatialSnapshot.CreateMatchSnapshot(geometry, match, 1, grid);
        var request = new EncounterRequest(
            new MatchRequestContext(match, Guid.NewGuid(), new PlayerId("test")),
            new DefinitionId("Normal"), 3, EncounterRequestSource.DebugWindow);

        var first = ((IEncounterExecutor)_launcher).Execute(request, snapshot, 1);
        var second = ((IEncounterExecutor)_launcher).Execute(request, snapshot, 2);

        Assert.That(first.ExecutionSequence, Is.EqualTo(1));
        Assert.That(second.ExecutionSequence, Is.EqualTo(2));
        Assert.That(first.RequestId, Is.EqualTo(request.Context.RequestId));
        Assert.That(_launcher.RecentDiagnostics.Where(item => item.Kind == GameDiagnosticKind.RequestStarted)
            .Select(item => item.RequestId), Is.EqualTo(new long[] { 11, 12 }));
    }

    [Test]
    public void RegionReader_ForwardsLatestSnapshotAndHandlesUnavailableSource()
    {
        Assert.That(ReadRegion(out _, out _), Is.False);
        var source = BindRegion(Region(0));
        Assert.That(ReadRegion(out var first, out _), Is.True);
        Assert.That(first, Is.SameAs(source.Snapshot));
        source.Snapshot = Region(4, match: "different-match");
        Assert.That(ReadRegion(out var latest, out _), Is.True);
        Assert.That(latest, Is.SameAs(source.Snapshot));
        source.Disposed = true;
        Assert.That(ReadRegion(out _, out string reason), Is.False);
        Assert.That(reason, Does.Contain("종료"));
    }

    [Test]
    public void Runtime_IsCreatedPerMatchAndReceivesCurrentInspectorSettings()
    {
        var enemies = ScriptableObject.CreateInstance<EnemyCatalog>();
        var squads = ScriptableObject.CreateInstance<EnemySquadPresetCatalog>();
        try
        {
            _root.AddComponent<NetworkObject>();
            var executor = _root.AddComponent<EnemySpawnExecutor>();
            typeof(EnemySpawnExecutor).GetField("enemyList", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(executor, enemies);
            Set("_spawnExecutor", executor);
            Set("_squadCatalog", squads);
            typeof(EnemyNetworkTestLauncher).GetMethod("EnsureEncounterRuntime", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_launcher, null);
            Assert.That(Get<EncounterRuntime>("_encounterRuntime"), Is.Null);

            _launcher.UseAppNetworkOwnership();
            var firstMatch = new MatchId("first");
            _launcher.BindAuthorityMatch(firstMatch, new PlayerId("test"), () => null);
            var first = Get<EncounterRuntime>("_encounterRuntime");
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Executor, Is.SameAs(executor));
            Set("spawnBudget", 30);
            typeof(EnemyNetworkTestLauncher).GetMethod("EnsureEncounterRuntime", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_launcher, null);
            Assert.That(Get<EncounterBudgetSettings>("_runtimeBudgetSettings").baseBudget, Is.EqualTo(30));
            _launcher.UnbindMatch(firstMatch);
            Assert.That(Get<EncounterRuntime>("_encounterRuntime"), Is.Null);

            var secondMatch = new MatchId("second");
            _launcher.BindAuthorityMatch(secondMatch, new PlayerId("test"), () => null);
            Assert.That(Get<EncounterRuntime>("_encounterRuntime"), Is.Not.SameAs(first));
            _launcher.UnbindMatch(secondMatch);
        }
        finally
        {
            Object.DestroyImmediate(squads);
            Object.DestroyImmediate(enemies);
        }
    }

    private bool ReadRegion(out RegionGridSnapshot region, out string reason)
    {
        object[] arguments = { null, null };
        bool success = (bool)typeof(EnemyNetworkTestLauncher)
            .GetMethod("TryReadRegion", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_launcher, arguments);
        region = (RegionGridSnapshot)arguments[0];
        reason = (string)arguments[1];
        return success;
    }

    private TestRegionSource BindRegion(RegionGridSnapshot snapshot)
    {
        var source = new TestRegionSource { Snapshot = snapshot };
        var binding = _root.AddComponent<RegionSceneBinding>();
        // 읽기 경계만 구성한다. Core의 지역 점령 정책은 실행하지 않는다.
        typeof(RegionSceneBinding).GetField("_source", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(binding, source);
        Set("regionBinding", binding);
        return source;
    }

    private static RegionGridSnapshot Region(int captured, RegionPolicyMode mode = RegionPolicyMode.Dynamic,
        string match = "launcher-test")
    {
        var geometry = new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 2, 2, 2, 2);
        return RegionGridSnapshot.FromCapturedCells(new MatchId(match), Guid.NewGuid(), 1, mode,
            new RegionGridDefinition(geometry), Enumerable.Range(0, captured));
    }

    private sealed class TestRegionSource : IRegionGridSource
    {
        private RegionGridSnapshot _snapshot;
        public bool Disposed;
        public RegionGridSnapshot Snapshot
        {
            get => Disposed ? throw new ObjectDisposedException(nameof(TestRegionSource)) : _snapshot;
            set => _snapshot = value;
        }
        public event Action<RegionGridSnapshot> Changed { add { } remove { } }
    }

    private T Get<T>(string name) => (T)Field(name).GetValue(_launcher);
    private void Set(string name, object value) => Field(name).SetValue(_launcher, value);
    private static FieldInfo Field(string name) => typeof(EnemyNetworkTestLauncher)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private sealed class TestStreams : IRandomStreams, IRandomSource
    {
        public string LastStream { get; private set; }
        public bool IsAvailable => true;
        public IRandomSource GetStream(string streamId) { LastStream = streamId; return this; }
        public uint NextUInt32() => throw new InvalidOperationException("Unexpected random consumption.");
        public int NextInt(int minInclusive, int maxExclusive) => throw new InvalidOperationException("Unexpected random consumption.");
        public float NextFloat() => throw new InvalidOperationException("Unexpected random consumption.");
        public float NextFloat(float minInclusive, float maxExclusive) => throw new InvalidOperationException("Unexpected random consumption.");
        public double NextDouble() => throw new InvalidOperationException("Unexpected random consumption.");
        public double NextDouble(double minInclusive, double maxExclusive) => throw new InvalidOperationException("Unexpected random consumption.");
    }
}

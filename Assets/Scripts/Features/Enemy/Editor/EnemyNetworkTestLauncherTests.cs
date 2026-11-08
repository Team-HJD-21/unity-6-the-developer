using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
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

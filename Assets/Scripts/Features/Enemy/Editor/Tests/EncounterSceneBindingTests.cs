using TeamHJD.Game.Enemies.Encounter;
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Bootstrap.SceneComposition;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 테스트런처의 Region 연결과 유효한 생성 요청 구성을 검사한다.
/// </summary>
public sealed class EncounterSceneBindingTests
{
    private GameObject _root;
    private EncounterSceneBinding _launcher;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Launcher test");
        _root.SetActive(false);
        _launcher = _root.AddComponent<EncounterSceneBinding>();
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
            typeof(EncounterSceneBinding).GetMethod("EnsurePipeline", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_launcher, null);
            Assert.That(Get<EncounterPipeline>("_pipeline"), Is.Null);

            _launcher.UseAppNetworkOwnership();
            var firstMatch = new MatchId("first");
            _launcher.BindAuthorityMatch(firstMatch, new PlayerId("test"), () => null);
            var first = Get<EncounterPipeline>("_pipeline");
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Executor, Is.SameAs(executor));
            Set("spawnBudget", 30);
            typeof(EncounterSceneBinding).GetMethod("EnsurePipeline", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_launcher, null);
            Assert.That(Get<EncounterBudgetSettings>("_runtimeBudgetSettings").baseBudget, Is.EqualTo(30));
            _launcher.UnbindMatch(firstMatch);
            Assert.That(Get<EncounterPipeline>("_pipeline"), Is.Null);

            var secondMatch = new MatchId("second");
            _launcher.BindAuthorityMatch(secondMatch, new PlayerId("test"), () => null);
            Assert.That(Get<EncounterPipeline>("_pipeline"), Is.Not.SameAs(first));
            _launcher.UnbindMatch(secondMatch);
        }
        finally
        {
            Object.DestroyImmediate(squads);
            Object.DestroyImmediate(enemies);
        }
    }

    [Test]
    public void RegionGroups_InitializeWhenCurrentMatchRegionBecomesAvailable()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(_root, scene);
        var enemies = ScriptableObject.CreateInstance<EnemyCatalog>();
        var squads = ScriptableObject.CreateInstance<EnemySquadPresetCatalog>();
        var match = new MatchId("region-groups");
        try
        {
            _root.AddComponent<NetworkObject>();
            var executor = _root.AddComponent<EnemySpawnExecutor>();
            _root.AddComponent<SpawnPoint>();
            typeof(EnemySpawnExecutor).GetField("enemyList", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(executor, enemies);
            Set("_spawnExecutor", executor);
            Set("_squadCatalog", squads);
            _launcher.UseAppNetworkOwnership();
            _launcher.BindAuthorityMatch(match, new PlayerId("test"), () => null);

            var registry = executor.SpawnPointList;
            Assert.That(registry.SetRegionSpawnEnabled(7, false), Is.False);
            var definition = new RegionGridDefinition(BattlefieldGridConfiguration.Default,
                Enumerable.Repeat(7, BattlefieldGridConfiguration.Default.TotalCellCount));
            var source = BindRegion(RegionGridSnapshot.FromCapturedCells(new MatchId("other"),
                Guid.NewGuid(), 0, RegionPolicyMode.Static, definition, Array.Empty<int>()));
            Assert.That(ReadRegion(out _, out _), Is.True);
            Assert.That(registry.SetRegionSpawnEnabled(7, false), Is.False);

            source.Snapshot = RegionGridSnapshot.FromCapturedCells(match, Guid.NewGuid(), 0,
                RegionPolicyMode.Static, definition, Array.Empty<int>());
            Assert.That(ReadRegion(out _, out _), Is.True);
            Assert.That(registry.SetRegionSpawnEnabled(7, false), Is.True);

            // 같은 Match에서 등록 목록이 바뀌어도 그룹과 차단 상태를 유지한다.
            var addedObject = new GameObject("Added spawn point");
            SceneManager.MoveGameObjectToScene(addedObject, scene);
            var addedPoint = addedObject.AddComponent<SpawnPoint>();
            Assert.That(executor.SpawnPointList, Is.SameAs(registry));
            Assert.That(registry.IsRegionGroupsInitialized, Is.True);
            Assert.That(registry.GetRegionGroups().Single().SpawnPoints.Count, Is.EqualTo(2));
            Assert.That(addedPoint.CanSpawnEnemies, Is.False);
            Assert.That(ReadRegion(out _, out _), Is.True);

            Assert.That(registry.SetRegionSpawnEnabled(7, true), Is.True);
            Assert.That(addedPoint.CanSpawnEnemies, Is.True);
            Object.DestroyImmediate(_root.GetComponent<SpawnPoint>());
            Assert.That(executor.SpawnPointList.GetRegionGroups().Single().SpawnPoints.Count, Is.EqualTo(1));

            // 마지막 지점이 사라져도 Region의 차단 설정을 보존한다.
            registry.SetRegionSpawnEnabled(7, false);
            Object.DestroyImmediate(addedPoint);
            Assert.That(executor.SpawnPointList.GetRegionGroups(), Is.Empty);
            addedPoint = addedObject.AddComponent<SpawnPoint>();
            Assert.That(executor.SpawnPointList.GetRegionGroups().Count, Is.EqualTo(1));
            Assert.That(addedPoint.CanSpawnEnemies, Is.False);

            // 새 Match에서는 이전 Region 제한을 넘기지 않는다.
            _launcher.UnbindMatch(match);
            Assert.That(registry.IsRegionGroupsInitialized, Is.False);
            Assert.That(addedPoint.CanSpawnEnemies, Is.True);
            match = new MatchId("next-region-groups");
            source.Snapshot = RegionGridSnapshot.FromCapturedCells(match, Guid.NewGuid(), 0,
                RegionPolicyMode.Static, definition, Array.Empty<int>());
            _launcher.BindAuthorityMatch(match, new PlayerId("test"), () => null);
            Assert.That(registry.IsRegionGroupsInitialized, Is.True);
            Assert.That(addedPoint.CanSpawnEnemies, Is.True);

            // Region 허용은 개별 지점에 설정된 차단을 덮어쓰지 않는다.
            addedPoint.SetEnemySpawnEnabled(false);
            registry.SetRegionSpawnEnabled(7, false);
            registry.SetRegionSpawnEnabled(7, true);
            Assert.That(addedPoint.CanSpawnEnemies, Is.False);
            registry.ResetRegionGroups();
            Assert.That(addedPoint.CanSpawnEnemies, Is.False);

            // Dynamic 전환은 Region 제한만 해제하고 개별 지점의 차단은 보존한다.
            var disabledObject = new GameObject("Individually disabled point");
            SceneManager.MoveGameObjectToScene(disabledObject, scene);
            var disabledPoint = disabledObject.AddComponent<SpawnPoint>();
            disabledPoint.SetEnemySpawnEnabled(false);
            addedPoint.SetEnemySpawnEnabled(true);
            Assert.That(ReadRegion(out _, out _), Is.True);
            Assert.That(registry.SetRegionSpawnEnabled(7, false), Is.True);
            Assert.That(addedPoint.CanSpawnEnemies, Is.False);

            source.Snapshot = RegionGridSnapshot.FromCapturedCells(new MatchId("other"),
                Guid.NewGuid(), 0, RegionPolicyMode.Dynamic, definition, Array.Empty<int>());
            Assert.That(ReadRegion(out _, out _), Is.True);
            Assert.That(registry.IsRegionGroupsInitialized, Is.True);
            Assert.That(addedPoint.CanSpawnEnemies, Is.False);

            source.Snapshot = RegionGridSnapshot.FromCapturedCells(match, Guid.NewGuid(), 0,
                RegionPolicyMode.Dynamic, definition, Array.Empty<int>());
            Assert.That(ReadRegion(out _, out _), Is.True);
            Assert.That(registry.IsRegionGroupsInitialized, Is.False);
            Assert.That(registry.GetRegionGroups(), Is.Empty);
            Assert.That(addedPoint.CanSpawnEnemies, Is.True);
            Assert.That(disabledPoint.CanSpawnEnemies, Is.False);

            source.Snapshot = RegionGridSnapshot.FromCapturedCells(match, Guid.NewGuid(), 0,
                RegionPolicyMode.Static, definition, Array.Empty<int>());
            Assert.That(ReadRegion(out _, out _), Is.True);
            Assert.That(registry.IsRegionGroupsInitialized, Is.True);
            Assert.That(addedPoint.CanSpawnEnemies, Is.True);
            Assert.That(disabledPoint.CanSpawnEnemies, Is.False);
        }
        finally
        {
            _launcher.UnbindMatch(match);
            Object.DestroyImmediate(squads);
            Object.DestroyImmediate(enemies);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private bool ReadRegion(out RegionGridSnapshot region, out string reason)
    {
        object[] arguments = { null, null };
        bool success = (bool)typeof(EncounterSceneBinding)
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
    private static FieldInfo Field(string name) => typeof(EncounterSceneBinding)
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

// Player 기능의 설정/해제 실패가 다른 기능·actor·Scene 자원 정리를 막지 않는지 검사합니다.
// 순수 제어 흐름 검사와 실제 Initial_Stage 종료 검사를 구분합니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Turrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerBindingCleanupTests
{
    public sealed class BindingProbe : MonoBehaviour, IPlayerMatchBinding
    {
        public bool ThrowOnBind, ThrowOnUnbind, Bound;
        public int UnbindCalls;
        public Action DuringUnbind;
        public void BindMatch(MatchSession session)
        {
            Bound = true;
            if (ThrowOnBind) throw new InvalidOperationException("Probe bind failure");
        }
        public void UnbindMatch()
        {
            Bound = false; UnbindCalls++;
            DuringUnbind?.Invoke();
            if (ThrowOnUnbind) throw new InvalidOperationException("Probe unbind failure");
        }
    }
    private sealed class ModeRules : IModeRules
    {
        public bool IsVictory(MatchState state) => false;
        public bool IsDefeat(MatchState state) => false;
        public MatchResult CreateResult(MatchState state, MatchOutcome outcome) =>
            new MatchResult(state.MatchId, outcome, 0, DateTimeOffset.UtcNow);
        public RewardReceipt CreateRewardReceipt(MatchResult result) =>
            new RewardReceipt(result.MatchId, result.Outcome, Array.Empty<CurrencyDelta>());
    }
    private sealed class Spawner : IPlayerActorSpawner
    {
        private readonly PlayerBindingCleanupTests _owner;
        public readonly List<BindingProbe> Probes = new List<BindingProbe>();
        public bool Disposed, FailBind, FailDispose;
        public Spawner(PlayerBindingCleanupTests owner) { _owner = owner; }
        public GameObject Spawn(PlayerId id, PlayerStart start)
        {
            var actor = _owner.Create("Cleanup actor " + id.Value);
            var probe = actor.AddComponent<BindingProbe>();
            probe.ThrowOnBind = FailBind;
            probe.ThrowOnUnbind = Probes.Count == 0;
            Probes.Add(probe); return actor;
        }
        public bool Despawn(PlayerId id) => false;
        public void Dispose()
        {
            Disposed = true;
            if (FailDispose) throw new InvalidOperationException("Probe spawner cleanup failure");
        }
    }
    private readonly List<GameObject> _objects = new List<GameObject>();
    private GameObject Create(string name)
    { var item = new GameObject(name); _objects.Add(item); return item; }
    private PlayerStart Start(int slot)
    {
        var start = Create("Cleanup PlayerStart " + slot).AddComponent<PlayerStart>();
        var serialized = new SerializedObject(start);
        serialized.FindProperty("slotIndex").intValue = slot; serialized.ApplyModifiedPropertiesWithoutUndo();
        return start;
    }
    private static MatchSession Match(int playerCount = 1)
    {
        var id = new MatchId("cleanup-test");
        var players = Enumerable.Range(0, playerCount).Select(i => new PlayerId("cleanup-player-" + i)).ToArray();
        var config = new MatchConfig(id, MatchMode.Cooperative, new StageId("test"), new DefinitionId("test"), "test", 1,
            players.Select(player => new PlayerLoadoutSnapshot(player, null)));
        var state = new MatchState(id, MatchPhase.Preparing, players.Select(player => new PlayerState(player, 1, 1, true)),
            new ControlUnitState(1, 1, 1, 1), new WaveState(0, 1, 0, 0), Array.Empty<TurretState>(), Array.Empty<EnemyState>(), Array.Empty<SectorState>());
        return new MatchSessionFactory(new LocalAuthority()).Create(config, state, new ModeRules());
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var item in _objects) if (item != null) Object.DestroyImmediate(item);
        _objects.Clear();
        if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
    }
    [Test]
    public void OneUnbindFailureStillReleasesOtherFeaturesAndClosesInput()
    {
        using var match = Match(); var actor = Create("Cleanup adapter");
        var adapter = actor.AddComponent<PlayerSceneAdapter>();
        var first = actor.AddComponent<BindingProbe>(); var second = actor.AddComponent<BindingProbe>();
        adapter.Bind(match); first.ThrowOnUnbind = true;
        var error = Assert.Throws<AggregateException>(() => adapter.Unbind());
        Assert.That(error.Flatten().InnerExceptions.Single().Message, Is.EqualTo("Probe unbind failure"));
        Assert.That(first.Bound || second.Bound, Is.False);
        Assert.That(first.UnbindCalls, Is.EqualTo(1)); Assert.That(second.UnbindCalls, Is.EqualTo(1));
        Assert.Throws<InvalidOperationException>(() => adapter.Submit(null));
        adapter.Unbind(); Assert.That(second.UnbindCalls, Is.EqualTo(1));
    }
    [Test]
    public void FailedBindRollsBackAndPreservesSetupAndCleanupErrors()
    {
        using var match = Match(); var actor = Create("Partial adapter");
        var adapter = actor.AddComponent<PlayerSceneAdapter>();
        var first = actor.AddComponent<BindingProbe>(); var failing = actor.AddComponent<BindingProbe>();
        failing.ThrowOnBind = failing.ThrowOnUnbind = true;
        var error = Assert.Throws<AggregateException>(() => adapter.Bind(match));
        Assert.That(error.Flatten().InnerExceptions.Select(item => item.Message), Is.EquivalentTo(new[] { "Probe bind failure", "Probe unbind failure" }));
        Assert.That(first.Bound || failing.Bound, Is.False);
        Assert.Throws<InvalidOperationException>(() => adapter.Submit(null));
        failing.ThrowOnBind = failing.ThrowOnUnbind = false;
        adapter.Bind(match); Assert.That(first.Bound && failing.Bound, Is.True);
        adapter.Unbind(); Assert.That(first.Bound || failing.Bound, Is.False);
    }
    [Test]
    public void CleanupRejectsRebindingButContinuesOtherFeatureRelease()
    {
        using var match = Match(); var actor = Create("Reentrant adapter");
        var adapter = actor.AddComponent<PlayerSceneAdapter>();
        var first = actor.AddComponent<BindingProbe>(); var second = actor.AddComponent<BindingProbe>();
        adapter.Bind(match); first.DuringUnbind = () => adapter.Bind(match);
        Assert.Throws<AggregateException>(() => adapter.Unbind());
        Assert.That(first.Bound || second.Bound, Is.False);
        Assert.That(second.UnbindCalls, Is.EqualTo(1));
        Assert.Throws<InvalidOperationException>(() => adapter.Submit(null));
    }
    [Test]
    public void ScopeCleansEveryActorAndSpawnerEvenWhenBothThrow()
    {
        using var match = Match(2); var spawner = new Spawner(this) { FailDispose = true };
        var scope = MatchPlayerActorScope.SpawnPlayers(match, spawner, new[] { Start(0), Start(1) });
        var error = Assert.Throws<AggregateException>(() => scope.Dispose());
        Assert.That(error.Flatten().InnerExceptions.Count, Is.EqualTo(2));
        Assert.That(spawner.Probes.All(probe => !probe.Bound && probe.UnbindCalls == 1), Is.True);
        Assert.That(spawner.Disposed, Is.True); Assert.That(scope.Actors, Is.Empty);
        Assert.DoesNotThrow(() => scope.Dispose());
    }
    [Test]
    public void ScopeSetupFailureRetainsActorForRollbackAndPreservesBothErrors()
    {
        using var match = Match(); var spawner = new Spawner(this) { FailBind = true, FailDispose = true };
        var error = Assert.Throws<AggregateException>(() => MatchPlayerActorScope.SpawnPlayers(match, spawner, new[] { Start(0) }));
        Assert.That(error.Flatten().InnerExceptions.Select(item => item.Message), Is.EquivalentTo(new[]
            { "Probe bind failure", "Probe unbind failure", "Probe spawner cleanup failure" }));
        Assert.That(spawner.Disposed, Is.True); Assert.That(spawner.Probes.Single().Bound, Is.False);
        Assert.That(spawner.Probes.Single().UnbindCalls, Is.EqualTo(1));
    }
    [UnityTest]
    public IEnumerator ActualSceneStillReleasesTurretsRegionsAndActorsAfterPlayerCallbackFails()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        yield return new EnterPlayMode(); yield return null;
        AssertSceneFailureCleanup();
        int frames = 0;
        float deadline = Time.realtimeSinceStartup + 2f;
        // Destroy/NGO Despawn은 프레임 끝에 확정됩니다. 잔존을 무시하지 않고 제한 시간 안에 0개를 요구합니다.
        while ((Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None).Length != 0 ||
            Object.FindObjectsByType<NetworkMatchAnchor>(FindObjectsSortMode.None).Length != 0) && Time.realtimeSinceStartup < deadline)
        { frames++; yield return null; }
        Assert.That(Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(Object.FindObjectsByType<NetworkMatchAnchor>(FindObjectsSortMode.None), Is.Empty);
        Debug.Log("PLAYER_CLEANUP_DESTROY_CONFIRMED frames=" + frames);
        yield return new ExitPlayMode();
    }
    private static void AssertSceneFailureCleanup()
    {
        var app = Object.FindAnyObjectByType<AppRoot>();
        var network = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        var actor = Object.FindAnyObjectByType<PlayerActorIdentity>().gameObject;
        var probe = actor.AddComponent<BindingProbe>(); probe.ThrowOnUnbind = true;
        Assert.Throws<AggregateException>(() => network.TryEndMatch());
        Assert.That(probe.UnbindCalls, Is.EqualTo(1));
        Assert.That(app.CurrentMatchId.IsEmpty, Is.True);
        Assert.That(network.Matches.Phase, Is.EqualTo(NetworkMatchPhase.Ended));
        Assert.That(network.TurretCommands, Is.Null); Assert.That(network.TurretPresentation, Is.Null);
        Assert.That(app.NetworkRegions.Source, Is.Null);
        Assert.That(Object.FindAnyObjectByType<TurretMatchBinding>().Count, Is.Zero);
        Assert.That(Object.FindAnyObjectByType<TurretController>().HasWriteAuthority, Is.False);
    }
}

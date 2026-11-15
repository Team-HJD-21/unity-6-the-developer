using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Presentation.Scene;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using EntityId = TeamHJD.Game.Domain.EntityId;

/// <summary>실제 네트워크 실행 없이 복원 완료 개체의 논리 ID 연결과 이벤트 구독을 검증한다.</summary>
public sealed class EnemySummonCheckpointTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> _objects = new();
    private EnemySpawnExecutor _executor;
    private IDictionary _tracked;

    [SetUp]
    public void SetUp()
    {
        _executor = NewObject("executor").AddComponent<EnemySpawnExecutor>();
        _tracked = (IDictionary)typeof(EnemySpawnExecutor).GetField("_spawnedEnemies", Private).GetValue(_executor);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var root in _objects)
        {
            foreach (var network in root.GetComponents<NetworkBehaviour>())
                SetNetwork(network, false, 0);
            Object.DestroyImmediate(root);
        }
        _objects.Clear();
    }

    [Test]
    public void ChildBeforeParent_RestoresProtectionAndDeathReleasesAssignment()
    {
        var child = AddEnemy("child", 100, "summon/parent");
        var parent = AddEnemy("parent", 200, "point");
        var generator = parent.gameObject.AddComponent<EnemySummonGenerator>();
        var rule = parent.gameObject.AddComponent<BossDamageRule>();
        typeof(BossDamageRule).GetField("summonGenerator", Private).SetValue(rule, generator);

        Resolve();

        Assert.That(generator.AliveSummonCount, Is.EqualTo(1));
        Assert.That(rule.IsProtectedBySummons, Is.True);
        Assert.That(rule.ModifyDamage(100, TeamHJD.Game.Enemy.Contracts.AttackSourceType.Player), Is.EqualTo(50));
        Notify(child, "DeathConfirmed");
        Assert.That(generator.AliveSummonCount, Is.Zero);
        Assert.That(rule.IsProtectedBySummons, Is.False);
        Assert.That(rule.ModifyDamage(100, TeamHJD.Game.Enemy.Contracts.AttackSourceType.Player), Is.EqualTo(100));
        Assert.That(SubscriberCount(child, "Despawned"), Is.Zero);
    }

    [Test]
    public void RepeatedResolve_DoesNotDoubleCountOrDuplicateSubscriptions()
    {
        var parent = AddEnemy("parent", 10, "point");
        var generator = parent.gameObject.AddComponent<EnemySummonGenerator>();
        var child = AddEnemy("child", 11, "summon/parent");
        Resolve();
        Resolve();
        Assert.That(generator.AliveSummonCount, Is.EqualTo(1));
        Assert.That(SubscriberCount(child, "DeathConfirmed"), Is.EqualTo(1));
        Assert.That(SubscriberCount(child, "Despawned"), Is.EqualTo(1));
        Notify(child, "Despawned");
        Assert.That(generator.AliveSummonCount, Is.Zero);
        Assert.That(SubscriberCount(child, "DeathConfirmed"), Is.Zero);
    }

    [Test]
    public void MissingParentAndRegularSpawn_AreNotAssignedToAnotherSummoner()
    {
        var parent = AddEnemy("parent", 20, "point");
        var generator = parent.gameObject.AddComponent<EnemySummonGenerator>();
        AddEnemy("orphan", 21, "summon/removed-parent");
        AddEnemy("regular", 22, "point");
        Resolve();
        Assert.That(generator.AliveSummonCount, Is.Zero);
    }

    [Test]
    public void SixRestoredChildren_AreAllCountedAgainstExistingLimit()
    {
        var parent = AddEnemy("parent", 30, "point");
        var generator = parent.gameObject.AddComponent<EnemySummonGenerator>();
        typeof(EnemySummonGenerator).GetField("maxAliveSummons", Private).SetValue(generator, 6);
        for (ulong i = 1; i <= 6; i++) AddEnemy("child" + i, 30 + i, "summon/parent");
        Resolve();
        Assert.That(generator.AliveSummonCount, Is.EqualTo(6));

        // 실제 Spawn 진입에서도 상한에 걸리면 생성기 조회까지 진행하지 않아야 한다.
        SetNetwork(generator, true, 30);
        typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(generator, true);
        var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        try
        {
            typeof(EnemyDefinition).GetField("_prefab", Private).SetValue(definition, parent.gameObject);
            Assert.That(generator.Spawn(definition, 2, null), Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }
        finally { Object.DestroyImmediate(definition); }
    }

    [Test]
    public void RecreatedActorsWithSameLogicalIds_ReconnectUsingNewNetworkIds()
    {
        var parent = AddEnemy("parent", 40, "point");
        var generator = parent.gameObject.AddComponent<EnemySummonGenerator>();
        var oldChild = AddEnemy("child", 41, "summon/parent");
        Resolve();
        generator.OnNetworkDespawn();
        Assert.That(SubscriberCount(oldChild, "DeathConfirmed"), Is.Zero);
        _tracked.Clear();
        var restoredParent = AddEnemy("parent", 400, "point");
        var restoredGenerator = restoredParent.gameObject.AddComponent<EnemySummonGenerator>();
        var restoredChild = AddEnemy("child", 401, "summon/parent");
        Resolve();
        Assert.That(restoredGenerator.AliveSummonCount, Is.EqualTo(1));
        Notify(oldChild, "DeathConfirmed");
        Assert.That(restoredGenerator.AliveSummonCount, Is.EqualTo(1));
        Notify(restoredChild, "DeathConfirmed");
        Assert.That(restoredGenerator.AliveSummonCount, Is.Zero);
    }

    private GameObject NewObject(string name)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        _objects.Add(root);
        return root;
    }

    private EnemyNetworkController AddEnemy(string logicalId, ulong networkId, string point)
    {
        var root = NewObject(logicalId);
        var ngo = root.AddComponent<NetworkObject>();
        root.AddComponent<SceneEntityIdentity>().Initialize(new EntityId(logicalId));
        root.AddComponent<EnemyHealth>();
        var actor = root.AddComponent<EnemyNetworkController>();
        // 복원 완료 시점의 NGO 식별 상태만 구성한다. Host나 네트워크를 시작하지 않는다.
        SetNetwork(actor, true, networkId);
        Type trackedType = typeof(EnemySpawnExecutor).GetNestedType("TrackedEnemy", BindingFlags.NonPublic);
        var tracked = Activator.CreateInstance(trackedType, new object[] { ngo, null, actor, null, null, "Slime", point });
        _tracked.Add(networkId, tracked);
        return actor;
    }

    private void Resolve() => typeof(EnemySpawnExecutor).GetMethod("ResolveCheckpointReferences", Private).Invoke(_executor, null);

    private static void SetNetwork(NetworkBehaviour behaviour, bool spawned, ulong id)
    {
        typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(behaviour, spawned);
        typeof(NetworkBehaviour).GetProperty("NetworkObjectId").SetValue(behaviour, id);
    }

    private static void Notify(EnemyNetworkController actor, string name)
    {
        var callback = (Action<ulong>)typeof(EnemyNetworkController).GetField(name, Private).GetValue(actor);
        callback?.Invoke(actor.NetworkObjectId);
    }

    private static int SubscriberCount(EnemyNetworkController actor, string name)
        => ((Delegate)typeof(EnemyNetworkController).GetField(name, Private).GetValue(actor))?.GetInvocationList().Length ?? 0;
}

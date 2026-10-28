using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerFieldTargetCollisionTests
{
    private readonly List<GameObject> _objects = new();
    private readonly Vector3 _origin = new(22000, 22000);
    private PlayerFieldDevice _barrier;

    [SetUp]
    public void CreateBarrier() => _barrier = BarrierAt(_origin);

    [TearDown]
    public void Cleanup()
    {
        foreach (var instance in _objects)
            if (instance != null) Object.DestroyImmediate(instance);
        _objects.Clear();
    }

    [TestCase(0f)]
    [TestCase(4f)]
    public void RouteThroughEitherEmitterOutsideMembrane_SelectsBarrier(float endpoint)
    {
        Vector2 start = _origin + new Vector3(endpoint - 1, .45f);
        Vector2 goal = start + Vector2.right * 2;
        Assert.That(_barrier.GetComponent<BoxCollider2D>().OverlapPoint(start + Vector2.right), Is.False);
        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(start, goal), Is.SameAs(_barrier));
    }

    [Test]
    public void RouteThroughEmptyPolygonCorner_DoesNotSelectBarrier()
    {
        Vector2 start = _origin + new Vector3(-.6f, .52f);
        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(start, start + Vector2.right * .2f), Is.Null);
    }

    [Test]
    public void EnemyBodyClipsEmitter_WhenCenterRouteMisses_StillSelectsBarrier()
    {
        Vector2 start = _origin + new Vector3(-1, .75f);
        Vector2 goal = start + Vector2.right * 2;
        var body = EnemyBodyAt(start);
        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(start, goal), Is.Null);
        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(start, goal, body), Is.SameAs(_barrier));
    }

    [Test]
    public void BodySweep_UsesRequestedRouteOriginWithoutMovingActor()
    {
        Vector2 start = _origin + new Vector3(-1, .75f);
        var body = EnemyBodyAt(_origin + new Vector3(10, 3));
        Vector3 actorPosition = body.transform.position;
        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(start, start + Vector2.right * 2, body), Is.SameAs(_barrier));
        Assert.That(body.transform.position, Is.EqualTo(actorPosition));
    }

    [Test]
    public void BodySweep_PreservesChildColliderOffsetFromRigidbodyRoot()
    {
        Vector2 start = _origin + new Vector3(-1, .75f);
        var rootBody = EnemyBodyAt(start);
        var child = new GameObject("Offset Body");
        child.layer = rootBody.gameObject.layer;
        child.transform.SetParent(rootBody.transform, false);
        child.transform.localPosition = Vector3.right * 3;
        var body = child.AddComponent<BoxCollider2D>();
        body.size = rootBody.size;
        rootBody.enabled = false;
        Physics2D.SyncTransforms();

        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(start, start + Vector2.right * 2, body), Is.SameAs(_barrier));
    }

    [Test]
    public void StationaryEnemyAlreadyBlockedByEmitter_SelectsBarrier()
    {
        Vector2 position = _origin + new Vector3(.8f, .4f);
        var body = EnemyBodyAt(position);
        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(position, position, body), Is.SameAs(_barrier));
    }

    [Test]
    public void DisabledEmitter_IsNotAnObstacleTarget()
    {
        foreach (var emitter in _barrier.GetComponentsInChildren<PolygonCollider2D>()) emitter.enabled = false;
        Vector2 start = _origin + new Vector3(-1, .45f);
        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(start, start + Vector2.right * 2), Is.Null);
    }

    [Test]
    public void MultipleBarriers_SelectsFirstIntersectedBody()
    {
        var second = BarrierAt(_origin + Vector3.right * 6);
        Vector2 start = _origin + new Vector3(12, .45f);
        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(start, _origin + new Vector3(-2, .45f)), Is.SameAs(second));
    }

    [Test]
    public void AttackingEitherEmitter_DamagesSharedHealthAndRemovesWholeBarrier()
    {
        var body = EnemyBodyAt(_origin);
        var behaviour = new PlayerFieldEnemyBehaviour();
        Assert.That(behaviour.Step(body.transform, body, _barrier, 1, .4f, 5, 1, 0, .1f, out _, out _), Is.True);
        body.transform.position = _origin + Vector3.right * 4;
        Assert.That(behaviour.Step(body.transform, body, _barrier, 1, .4f, 5, 1, 1.1f, .1f, out _, out _), Is.True);
        Assert.That(_barrier.Health, Is.EqualTo(290));
        _barrier.TakeDamage(290);
        Assert.That(_barrier == null, Is.True);
        Assert.That(PlayerFieldDeviceRegistry.ResolveTarget(_origin + Vector3.left, _origin + Vector3.right * 5), Is.Null);
    }

    private PlayerFieldDevice BarrierAt(Vector3 start)
    {
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/EnergyBarrier.prefab"));
        _objects.Add(instance);
        var device = instance.GetComponent<PlayerFieldDevice>();
        device.Initialize(PlayerFieldDeviceKind.Barrier, start, start + Vector3.right * 4, .3f, 300, 8);
        Physics2D.SyncTransforms();
        return device;
    }

    private BoxCollider2D EnemyBodyAt(Vector3 position)
    {
        var actor = new GameObject("Enemy Body", typeof(Rigidbody2D), typeof(BoxCollider2D));
        _objects.Add(actor);
        actor.layer = LayerMask.NameToLayer("Enemy");
        actor.transform.position = position;
        actor.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var body = actor.GetComponent<BoxCollider2D>();
        body.size = new Vector2(.4f, .8f);
        Physics2D.SyncTransforms();
        return body;
    }
}

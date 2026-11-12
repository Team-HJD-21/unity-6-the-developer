using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerBombPlacementTests
{
    private readonly List<GameObject> _objects = new();
    private readonly Vector3 _origin = new(23000, 23000);
    private GameObject _prefab;
    private GameObject _owner;

    [SetUp]
    public void CreateScenario()
    {
        _prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/bomb.prefab");
        _owner = Body("Owner", "PlayerFeetBox").gameObject;
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var instance in _objects)
            if (instance != null) Object.DestroyImmediate(instance);
        _objects.Clear();
    }

    [Test]
    public void InstallerAndItsDirectionalBodies_DoNotBlockFootPlacement()
    {
        var child = Body("Directional body", "PlayerFeetBox");
        child.transform.SetParent(_owner.transform, false);
        Assert.That(Evaluate(), Is.EqualTo(PlayerBombPlacementResult.Allowed));
    }

    [Test]
    public void EnemyBody_BlocksDespiteDifferentSortingZAndIgnoredPlayerCollision()
    {
        var enemy = Body("Enemy", "Enemy");
        enemy.transform.position += Vector3.forward * 65;
        bool ignored = Physics2D.GetIgnoreLayerCollision(_owner.layer, enemy.gameObject.layer);
        try
        {
            Physics2D.IgnoreLayerCollision(_owner.layer, enemy.gameObject.layer, true);
            Assert.That(Evaluate(), Is.EqualTo(PlayerBombPlacementResult.OverlapsEnemy));
            enemy.isTrigger = true;
            Assert.That(Evaluate(), Is.EqualTo(PlayerBombPlacementResult.Allowed));
        }
        finally { Physics2D.IgnoreLayerCollision(_owner.layer, enemy.gameObject.layer, ignored); }
    }

    [Test]
    public void ObstacleBlocksButCurrentFrameMovementOrTriggerAllowsPlacement()
    {
        var wall = Body("Wall", "Default");
        wall.includeLayers = 1 << _owner.layer;
        Assert.That(Evaluate(), Is.EqualTo(PlayerBombPlacementResult.BlockedByObstacle));
        wall.isTrigger = true;
        Assert.That(Evaluate(), Is.EqualTo(PlayerBombPlacementResult.Allowed));
        wall.isTrigger = false;
        wall.transform.position += Vector3.right * 10;
        Assert.That(Evaluate(), Is.EqualTo(PlayerBombPlacementResult.Allowed));
    }

    [Test]
    public void ExplicitBodyInclude_BlocksEvenWhenGlobalLayersAreIgnored()
    {
        var device = Body("Device", "PlayerFieldDevice");
        device.includeLayers = 1 << _owner.layer;
        bool ignored = Physics2D.GetIgnoreLayerCollision(_owner.layer, device.gameObject.layer);
        try
        {
            Physics2D.IgnoreLayerCollision(_owner.layer, device.gameObject.layer, true);
            Assert.That(Evaluate(), Is.EqualTo(PlayerBombPlacementResult.BlockedByObstacle));
            device.excludeLayers = 1 << _owner.layer;
            Assert.That(Evaluate(), Is.EqualTo(PlayerBombPlacementResult.Allowed));
        }
        finally { Physics2D.IgnoreLayerCollision(_owner.layer, device.gameObject.layer, ignored); }
    }

    [TestCase("Bullet")]
    [TestCase("EmenyBullet")]
    [TestCase("PlayerHitBox")]
    public void ProjectilesAndHitBoxes_DoNotBlockPlacement(string layer)
    {
        Body("Projectile", layer);
        Assert.That(Evaluate(), Is.EqualTo(PlayerBombPlacementResult.Allowed));
    }

    [Test]
    public void Footprint_MatchesAuthoredPolygonBoundsWithOffsetAndMirroredScale()
    {
        var instance = Object.Instantiate(_prefab, _origin, Quaternion.identity);
        _objects.Add(instance);
        instance.transform.localScale = new Vector3(-2, 3, 1);
        var body = instance.GetComponent<PolygonCollider2D>();
        body.offset = new Vector2(.2f, -.3f);
        Physics2D.SyncTransforms();

        Assert.That(PlayerBombPlacementRules.TryGetFootprint(instance, _origin, out Bounds footprint), Is.True);
        Assert.That(Vector2.Distance(footprint.center, body.bounds.center), Is.LessThan(.005f));
        Assert.That(Vector2.Distance(footprint.size, body.bounds.size), Is.LessThan(.005f));
    }

    [Test]
    public void DisabledOrMissingBody_FailsClosed()
    {
        var invalid = new GameObject("Invalid bomb");
        _objects.Add(invalid);
        Assert.That(PlayerBombPlacementRules.Evaluate(invalid, _origin, _owner), Is.EqualTo(PlayerBombPlacementResult.InvalidBody));
        var body = invalid.AddComponent<PolygonCollider2D>();
        body.enabled = false;
        Assert.That(PlayerBombPlacementRules.Evaluate(invalid, _origin, _owner), Is.EqualTo(PlayerBombPlacementResult.InvalidBody));
    }

    private PlayerBombPlacementResult Evaluate() => PlayerBombPlacementRules.Evaluate(_prefab, _origin, _owner);

    private BoxCollider2D Body(string name, string layer)
    {
        var instance = new GameObject(name, typeof(BoxCollider2D));
        _objects.Add(instance);
        instance.layer = LayerMask.NameToLayer(layer);
        instance.transform.position = _origin;
        return instance.GetComponent<BoxCollider2D>();
    }
}

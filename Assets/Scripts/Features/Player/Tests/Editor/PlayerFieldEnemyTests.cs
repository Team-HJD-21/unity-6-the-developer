using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerFieldEnemyTests
{
    private GameObject _actor, _beacon;
    private PlayerFieldDevice _device;
    private BoxCollider2D _body;
    private Collider2D _beaconBody;
    [SetUp]
    public void CreateScenario()
    {
        _actor = new GameObject("Actor");
        _actor.transform.position = new Vector3(4000, 4000, 0);
        _body = _actor.AddComponent<BoxCollider2D>();
        _body.size = new Vector2(.8f, .296f);
        _body.offset = new Vector2(0, -.227f);
        _beacon = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/LureBeacon.prefab"));
        _device = _beacon.GetComponent<PlayerFieldDevice>();
        _beaconBody = _beacon.GetComponentInChildren<PolygonCollider2D>();
        Vector3 point = _actor.transform.position + Vector3.right * 3;
        _device.Initialize(PlayerFieldDeviceKind.Beacon, point, point, .3f, 80, 8, 12);
        Physics2D.SyncTransforms();
    }
    [TearDown]
    public void Cleanup() { Object.DestroyImmediate(_actor); Object.DestroyImmediate(_beacon); }
    [Test]
    public void MissingNetworkDriver_CannotAcquireControl() => Assert.That(new PlayerFieldEnemyControlLease(_actor).TryAcquire(), Is.False);
    [Test]
    public void UnspawnedNetworkEnemy_KeepsOriginalDriverEnabled()
    {
        var driver = _actor.AddComponent<EnemyNetworkController>();
        var control = new PlayerFieldEnemyControlLease(_actor);
        Assert.That(control.TryAcquire(), Is.False);
        Assert.That(driver.enabled, Is.True);
    }
    [Test]
    public void Movement_StopsAtAttackRangeWithoutDamagingDevice()
    {
        new PlayerFieldEnemyBehaviour().Step(_actor.transform, _body, _device, 10, .4f, 5, 1, 0, 1, out _, out _);
        Physics2D.SyncTransforms();
        Assert.That(_body.Distance(_beaconBody).distance, Is.EqualTo(.4f).Within(.002f));
        Assert.That(_device.Health, Is.EqualTo(80).Within(.001f));
    }
    [Test]
    public void Attack_CannotRepeatWithinCooldown()
    {
        _actor.transform.position = _device.transform.position;
        var behaviour = new PlayerFieldEnemyBehaviour();
        Assert.That(behaviour.Step(_actor.transform, _body, _device, 1.5f, .4f, 5, 1, 0, .1f, out _, out _), Is.True);
        Assert.That(behaviour.Step(_actor.transform, _body, _device, 1.5f, .4f, 5, 1, .5f, .1f, out _, out _), Is.False);
        Assert.That(_device.Health, Is.EqualTo(75).Within(.001f));
    }

    [Test]
    public void DiagonalContact_WithOffsetBody_AttacksDespiteDistantActorOrigin()
    {
        _actor.transform.position = _device.transform.position + new Vector3(1, 1);
        Physics2D.SyncTransforms();
        var separation = _body.Distance(_beaconBody);
        _actor.transform.position -= (Vector3)separation.normal * (separation.distance - .02f);
        Physics2D.SyncTransforms();
        Assert.That(_body.Distance(_beaconBody).distance, Is.EqualTo(.02f).Within(.002f));
        Assert.That(Vector2.Distance(_actor.transform.position, _device.ApproachPoint(_actor.transform.position)), Is.GreaterThan(.4f));

        bool attacked = new PlayerFieldEnemyBehaviour().Step(_actor.transform, _body, _device,
            1.5f, .4f, 5, 1, 0, .1f, out _, out bool moving);

        Assert.That(attacked, Is.True);
        Assert.That(moving, Is.False);
        Assert.That(_device.Health, Is.EqualTo(75));
    }

    [Test]
    public void MovementThenAttack_UsesCurrentTransformWithoutWaitingForPhysicsStep()
    {
        var behaviour = new PlayerFieldEnemyBehaviour();
        behaviour.Step(_actor.transform, _body, _device, 10, .4f, 5, 1, 0, 1, out _, out _);
        Vector3 stopped = _actor.transform.position;

        Assert.That(behaviour.Step(_actor.transform, _body, _device,
            10, .4f, 5, 1, .1f, .1f, out _, out bool moving), Is.True);
        Assert.That(moving, Is.False);
        Assert.That(_actor.transform.position, Is.EqualTo(stopped));
    }

    [Test]
    public void TriggerHitbox_IsNotUsedAsAttackBody()
    {
        _body.isTrigger = true;
        _actor.transform.position = _device.transform.position;
        Assert.That(new PlayerFieldEnemyBehaviour().Step(_actor.transform, _body, _device,
            1.5f, .4f, 5, 1, 0, .1f, out _, out _), Is.False);
        Assert.That(_device.Health, Is.EqualTo(80));
    }

    [Test]
    public void DisabledDeviceCollider_CannotReceiveContactAttack()
    {
        _beaconBody.enabled = false;
        _actor.transform.position = _device.transform.position;
        Assert.That(new PlayerFieldEnemyBehaviour().Step(_actor.transform, _body, _device,
            1.5f, .4f, 5, 1, 0, .1f, out _, out _), Is.False);
        Assert.That(_device.Health, Is.EqualTo(80));
    }
}

using NUnit.Framework;
using UnityEngine;

public sealed class PlayerFieldEnemyTests
{
    private GameObject _actor, _beacon;
    private PlayerFieldDevice _device;
    [SetUp]
    public void CreateScenario()
    {
        _actor = new GameObject("Actor");
        _actor.transform.position = new Vector3(4000, 4000, 0);
        _beacon = new GameObject("Beacon", typeof(PlayerFieldDevice));
        _device = _beacon.GetComponent<PlayerFieldDevice>();
        Vector3 point = _actor.transform.position + Vector3.right * 3;
        _device.Initialize(PlayerFieldDeviceKind.Beacon, point, point, .3f, 80, 8, 12);
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
        new PlayerFieldEnemyBehaviour().Step(_actor.transform, _device, 10, .4f, 5, 1, 0, 1, out _, out _);
        Assert.That(Vector2.Distance(_actor.transform.position, _device.transform.position), Is.EqualTo(.4f).Within(.001f));
        Assert.That(_device.Health, Is.EqualTo(80).Within(.001f));
    }
    [Test]
    public void Attack_CannotRepeatWithinCooldown()
    {
        _actor.transform.position = _device.transform.position;
        var behaviour = new PlayerFieldEnemyBehaviour();
        Assert.That(behaviour.Step(_actor.transform, _device, 1.5f, .4f, 5, 1, 0, .1f, out _, out _), Is.True);
        Assert.That(behaviour.Step(_actor.transform, _device, 1.5f, .4f, 5, 1, .5f, .1f, out _, out _), Is.False);
        Assert.That(_device.Health, Is.EqualTo(75).Within(.001f));
    }
}
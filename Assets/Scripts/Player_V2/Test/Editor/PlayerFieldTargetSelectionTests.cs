using NUnit.Framework;
using UnityEngine;

public sealed class PlayerFieldTargetSelectionTests
{
    private GameObject _wall, _beacon;
    private readonly Vector3 _origin = new Vector3(1000, 1000, 0);
    [SetUp]
    public void CreateDevices()
    {
        _wall = new GameObject("Wall", typeof(PlayerFieldDevice));
        _wall.GetComponent<PlayerFieldDevice>().Initialize(PlayerFieldDeviceKind.Barrier,
            _origin + Vector3.left * 2, _origin + Vector3.right * 2, .3f, 300, 8);
        _beacon = new GameObject("Beacon", typeof(PlayerFieldDevice));
        _beacon.GetComponent<PlayerFieldDevice>().Initialize(PlayerFieldDeviceKind.Beacon,
            _origin + Vector3.up * 3, _origin + Vector3.up * 3, .3f, 80, 8, 12);
        Physics2D.SyncTransforms();
    }
    [TearDown]
    public void Cleanup() { Object.DestroyImmediate(_wall); Object.DestroyImmediate(_beacon); }
    [Test]
    public void CrossingBarrier_TakesPriorityOverBeacon() => Assert.That(
        PlayerFieldDevice.Resolve(_origin + Vector3.down * 3, _origin + Vector3.down * 4), Is.EqualTo(_wall.GetComponent<PlayerFieldDevice>()));
    [Test]
    public void DestroyedBarrier_AllowsBeaconSelection()
    {
        _wall.GetComponent<PlayerFieldDevice>().TakeDamage(300);
        Assert.That(PlayerFieldDevice.Resolve(_origin + Vector3.down * 3, _origin + Vector3.down * 4), Is.EqualTo(_beacon.GetComponent<PlayerFieldDevice>()));
    }
    [Test]
    public void RouteOutsideBarrier_IsNotBlocked()
    {
        _beacon.SetActive(false);
        Assert.That(PlayerFieldDevice.Resolve(_origin + new Vector3(4, -3), _origin + new Vector3(4, 3)), Is.Null);
    }
    [Test]
    public void Beacon_DoesNotAttractBeyondRadius()
    {
        Assert.That(PlayerFieldDevice.Resolve(_origin + Vector3.up * 16, _origin + Vector3.up * 17), Is.Null);
    }
}
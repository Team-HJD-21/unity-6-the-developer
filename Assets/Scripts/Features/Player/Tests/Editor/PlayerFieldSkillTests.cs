using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Editor 전용: 기존 프리팹을 로드하고 검증용 인스턴스만 생성합니다.
public sealed class PlayerFieldSkillTests
{
    private GameObject _instance;
    private PlayerFieldDevice _device;
    private readonly Vector3 _start = new Vector3(5000, 5000, 0);
    private Vector3 End => _start + new Vector3(3, 2, 0);
    [SetUp]
    public void CreateBarrier()
    {
        _instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/EnergyBarrier.prefab"));
        _device = _instance.GetComponent<PlayerFieldDevice>();
        _device.Initialize(PlayerFieldDeviceKind.Barrier, _start, End, .3f, 300, 8);
        Physics2D.SyncTransforms();
    }
    [TearDown]
    public void Cleanup() { if (_instance != null) Object.DestroyImmediate(_instance); }
    [Test]
    public void Emitters_ArePlacedAtEndpoints()
    {
        var bodies = _instance.GetComponentsInChildren<SpriteRenderer>();
        Assert.That(bodies.Length, Is.EqualTo(2));
        Assert.That(Vector3.Distance(bodies[0].transform.position, _start), Is.LessThan(.001f));
        Assert.That(Vector3.Distance(bodies[1].transform.position, End), Is.LessThan(.001f));
    }
    [Test]
    public void Emitters_KeepUprightRotation()
    {
        foreach (var body in _instance.GetComponentsInChildren<SpriteRenderer>())
            Assert.That(Quaternion.Angle(body.transform.rotation, Quaternion.identity), Is.LessThan(.001f));
    }
    [Test]
    public void VisualEnlargement_DoesNotScaleCollisionRoot()
    {
        Assert.That(Vector3.Distance(_instance.transform.localScale, Vector3.one), Is.LessThan(.001f));
        foreach (var body in _instance.GetComponentsInChildren<SpriteRenderer>())
            Assert.That(body.transform.localScale.x, Is.EqualTo(2.5f).Within(.001f));
    }
    [Test]
    public void Membrane_FollowsEndpointsWithGroundOffset()
    {
        var membrane = _instance.transform.Find("Energy Membrane").GetComponent<LineRenderer>();
        Assert.That(membrane.useWorldSpace, Is.True);
        Assert.That(Vector3.Distance(membrane.GetPosition(0), _start + Vector3.up * .3f), Is.LessThan(.001f));
        Assert.That(Vector3.Distance(membrane.GetPosition(1), End + Vector3.up * .3f), Is.LessThan(.001f));
    }
    [Test]
    public void Barrier_KeepsOriginalVisualAndCollisionThickness()
    {
        var membrane = _instance.transform.Find("Energy Membrane").GetComponent<LineRenderer>();
        var blocker = _instance.GetComponent<BoxCollider2D>();
        Assert.That(membrane.startWidth, Is.EqualTo(.48f).Within(.001f));
        Assert.That(blocker.size.y, Is.EqualTo(.3f).Within(.001f));
        Assert.That(blocker.size.x, Is.EqualTo(Vector3.Distance(_start, End)).Within(.001f));
    }
    [TestCase("PlayerFeetBox", true)]
    [TestCase("Bullet", true)]
    [TestCase("PlayerHitBox", true)]
    [TestCase("EnemyHitBox", true)]
    [TestCase("EnemyFeetBox", false)]
    public void Collision_OnlyBlocksEnemies(string layerName, bool passes)
    {
        int layer = LayerMask.NameToLayer(layerName);
        Assert.That(layer, Is.GreaterThanOrEqualTo(0));
        bool excluded = (_instance.GetComponent<BoxCollider2D>().excludeLayers.value & (1 << layer)) != 0;
        Assert.That(excluded, Is.EqualTo(passes));
    }
    [Test]
    public void DisabledDevice_IsRemovedFromTargetSelection()
    {
        _instance.SetActive(false);
        Assert.That(PlayerFieldDevice.Resolve(_start + Vector3.down, End + Vector3.up), Is.Null);
    }
    [TestCase(-1)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void Damage_RejectsInvalidValues(float damage)
    {
        _device.TakeDamage(damage);
        Assert.That(_device.Health, Is.EqualTo(300).Within(.001f));
    }
    [Test]
    public void Damage_DestroysDepletedDevice()
    {
        _device.TakeDamage(300);
        Assert.That(_instance == null, Is.True);
    }
}
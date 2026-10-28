using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PlayerDeviceCollisionTests
{
    private GameObject _device;
    private GameObject _player;
    private GameObject _hud;
    private readonly Vector3 _origin = new(18000f, 18000f);

    [TearDown]
    public void Cleanup()
    {
        if (_device != null) Object.DestroyImmediate(_device);
        if (_player != null) Object.DestroyImmediate(_player);
        if (_hud != null) Object.DestroyImmediate(_hud);
    }

    [TestCase("EnergyBarrier", 2)]
    [TestCase("LureBeacon", 1)]
    public void PrefabBodies_AreSolidAndOnlyBlockPlayerAndEnemy(string name, int expectedBodies)
    {
        _device = Object.Instantiate(Prefab(name));
        var bodies = _device.GetComponentsInChildren<PolygonCollider2D>();
        Assert.That(bodies.Length, Is.EqualTo(expectedBodies));
        int allowed = (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemy"));
        foreach (var body in bodies)
        {
            Assert.That(body.enabled, Is.True);
            Assert.That(body.isTrigger, Is.False);
            Assert.That(body.includeLayers.value, Is.EqualTo(allowed));
            Assert.That(body.excludeLayers.value, Is.EqualTo(~allowed));
            Assert.That(body.layerOverridePriority, Is.EqualTo(10));
            Assert.That(body.GetComponent<SpriteRenderer>(), Is.Not.Null);
        }
    }

    [Test]
    public void InitialOverlap_AllowsExitAcrossFacingChangeThenRestoresCollision()
    {
        _player = Object.Instantiate(Prefab("Player"), _origin + Vector3.up * .8f, Quaternion.identity);
        var selector = _player.GetComponent<PlayerDirectionalColliders>();
        selector.SetFacing(PlayerAimFacing.Down);
        _device = Object.Instantiate(Prefab("LureBeacon"));
        var field = _device.GetComponent<PlayerFieldDevice>();
        field.Initialize(PlayerFieldDeviceKind.Beacon, _origin, _origin, .3f, 80f, 8f, 12f);
        var body = _device.GetComponentInChildren<PolygonCollider2D>();
        var passage = _device.GetComponent<PlayerFieldBodyCollision>();
        Assert.That(Physics2D.GetIgnoreCollision(body, ActiveBody()), Is.True);

        selector.SetFacing(PlayerAimFacing.UpRight);
        Physics2D.SyncTransforms();
        Invoke(passage, "FixedUpdate");
        Assert.That(Physics2D.GetIgnoreCollision(body, ActiveBody()), Is.True);

        _player.transform.position = _origin + Vector3.up * 5f;
        Physics2D.SyncTransforms();
        Invoke(passage, "FixedUpdate");
        Assert.That(Physics2D.GetIgnoreCollision(body, ActiveBody()), Is.False);
        // Re-entering must not receive another exit exception.
        _player.transform.position = _origin + Vector3.up * .8f;
        Physics2D.SyncTransforms();
        Invoke(passage, "FixedUpdate");
        Assert.That(Physics2D.GetIgnoreCollision(body, ActiveBody()), Is.False);
    }

    [Test]
    public void BeaconDepth_ChangesAtPlayerFeetWithoutMovingPhysics()
    {
        _player = Object.Instantiate(Prefab("Player"), _origin + Vector3.down * 3f, Quaternion.identity);
        _player.GetComponent<PlayerDirectionalColliders>().SetFacing(PlayerAimFacing.Down);
        _device = Object.Instantiate(Prefab("LureBeacon"));
        _device.GetComponent<PlayerFieldDevice>().Initialize(PlayerFieldDeviceKind.Beacon,
            _origin, _origin, .3f, 80f, 8f, 12f, _player.transform);
        var sorting = _device.GetComponent<SortingGroup>();
        int playerOrder = _player.GetComponent<SpriteRenderer>().sortingOrder;
        Assert.That(sorting.sortingOrder, Is.EqualTo(playerOrder - 1));
        Assert.That(_device.transform.position, Is.EqualTo(_origin));

        _player.transform.position = _origin + Vector3.up * 3f;
        Physics2D.SyncTransforms();
        _device.GetComponent<PlayerSkillDepth>().Refresh();
        Assert.That(sorting.sortingOrder, Is.EqualTo(playerOrder + 1));
        Assert.That(_device.transform.position, Is.EqualTo(_origin));
    }

    [Test]
    public void BombDepth_ChangesAtPlayerFeetWithoutMovingPhysics()
    {
        _player = Object.Instantiate(Prefab("Player"), _origin + Vector3.down * 3f, Quaternion.identity);
        _player.GetComponent<PlayerDirectionalColliders>().SetFacing(PlayerAimFacing.Down);
        _device = Object.Instantiate(Prefab("bomb"), _origin, Quaternion.identity);
        Physics2D.SyncTransforms();
        _device.GetComponent<PlayerBombExplosion>().BindOwner(_player.transform);
        var sorting = _device.GetComponent<SortingGroup>();
        int playerOrder = _player.GetComponent<SpriteRenderer>().sortingOrder;
        Assert.That(sorting.sortingOrder, Is.EqualTo(playerOrder - 1));

        _player.transform.position = _origin + Vector3.up * 3f;
        Physics2D.SyncTransforms();
        _device.GetComponent<PlayerSkillDepth>().Refresh();
        Assert.That(sorting.sortingOrder, Is.EqualTo(playerOrder + 1));
        Assert.That(_device.transform.position, Is.EqualTo(_origin));
    }

    [Test]
    public void BarrierEmitters_SortIndependentlyAtEachEndpoint()
    {
        _player = Object.Instantiate(Prefab("Player"), _origin, Quaternion.identity);
        _player.GetComponent<PlayerDirectionalColliders>().SetFacing(PlayerAimFacing.Down);
        _device = Object.Instantiate(Prefab("EnergyBarrier"));
        _device.GetComponent<PlayerFieldDevice>().Initialize(PlayerFieldDeviceKind.Barrier,
            _origin + new Vector3(-3, -3), _origin + new Vector3(3, 3), .3f, 300f, 8f, owner: _player.transform);
        int order = _player.GetComponent<SpriteRenderer>().sortingOrder;
        Assert.That(_device.transform.Find("First Emitter").GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(order + 1));
        Assert.That(_device.transform.Find("Second Emitter").GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(order - 1));
    }

    [Test]
    public void CombatHud_FoldsToTenPixelEdgeAndReturnsToAuthoredPosition()
    {
        _hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab"));
        var layout = _hud.GetComponent<PlayerCombatHudLayout>();
        Invoke(layout, "Awake");
        var data = new SerializedObject(layout);
        var weapons = (RectTransform)data.FindProperty("_weapons").objectReferenceValue;
        var dash = (RectTransform)data.FindProperty("_dash").objectReferenceValue;
        var group = (CanvasGroup)data.FindProperty("_weaponInput").objectReferenceValue;
        Vector2 weaponHome = weapons.anchoredPosition, dashHome = dash.anchoredPosition;
        layout.Refresh(true, 1f);
        Assert.That(weapons.anchoredPosition.y + weapons.rect.height * (1f - weapons.pivot.y), Is.EqualTo(10f).Within(.001f));
        Assert.That(dash.anchoredPosition.y + dash.rect.height * (1f - dash.pivot.y), Is.EqualTo(10f).Within(.001f));
        Assert.That(group.blocksRaycasts, Is.False);
        Assert.That(group.interactable, Is.False);
        layout.Refresh(false, 1f);
        Assert.That(weapons.anchoredPosition, Is.EqualTo(weaponHome));
        Assert.That(dash.anchoredPosition, Is.EqualTo(dashHome));
        layout.Refresh(true, .08f);
        Invoke(layout, "OnDisable");
        Assert.That(weapons.anchoredPosition, Is.EqualTo(weaponHome));
        Assert.That(dash.anchoredPosition, Is.EqualTo(dashHome));
    }

    private Collider2D ActiveBody() => _player.GetComponentsInChildren<CapsuleCollider2D>().Single(c => c.enabled);
    private static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Player/{name}.prefab");
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}

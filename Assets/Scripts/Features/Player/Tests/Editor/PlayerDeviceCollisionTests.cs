using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
        int allowed = LayerMask.GetMask("PlayerFeetBox", "EnemyFeetBox");
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


    [UnityTest]
    public IEnumerator InstalledDevice_BlocksEnemyFeet([Values("EnergyBarrier", "LureBeacon", "bomb", "PlayerTurret")] string name)
    {
        yield return new EnterPlayMode();
        var scene = SceneManager.CreateScene("Device collision test", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        GameObject enemy = null;
        try
        {
            _device = Object.Instantiate(Prefab(name));
            SceneManager.MoveGameObjectToScene(_device, scene);
            if (_device.TryGetComponent(out PlayerFieldDevice field))
            {
                if (name == "EnergyBarrier")
                    field.Initialize(PlayerFieldDeviceKind.Barrier, Vector3.left * 4f, Vector3.right * 4f, .3f, 300f, 8f);
                else
                    field.Initialize(PlayerFieldDeviceKind.Beacon, Vector3.zero, Vector3.zero, .3f, 80f, 8f);
            }
            else if (_device.TryGetComponent(out PlayerTurret turret))
                turret.enabled = false;

            enemy = new GameObject("Enemy Feet");
            SceneManager.MoveGameObjectToScene(enemy, scene);
            enemy.layer = LayerMask.NameToLayer("EnemyFeetBox");
            var feet = enemy.AddComponent<BoxCollider2D>();
            feet.size = Vector2.one * .2f;
            var body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            var blocker = name == "EnergyBarrier" ? _device.GetComponent<BoxCollider2D>() :
                _device.GetComponentsInChildren<Collider2D>().Single(c => c.enabled && !c.isTrigger);
            Assert.That(blocker.enabled, Is.True);
            Vector2 center = blocker.bounds.center;
            body.position = center + Vector2.down * (blocker.bounds.extents.y + .5f);
            Physics2D.SyncTransforms();
            var physics = scene.GetPhysicsScene2D();
            for (int step = 0; step < 25; step++)
            {
                body.linearVelocity = Vector2.up * 4f;
                physics.Simulate(.02f);
            }

            Assert.That(feet.IsTouching(blocker), Is.True, name);
            Assert.That(body.position.y, Is.LessThan(center.y), name);
        }
        finally
        {
            if (enemy != null) Object.DestroyImmediate(enemy);
            if (_device != null) Object.DestroyImmediate(_device);
            SceneManager.UnloadSceneAsync(scene);
        }
        yield return new ExitPlayMode();
    }

    [Test]
    public void InitialOverlap_AllowsExitAcrossFacingChangeThenRestoresCollision()
    {
        _player = Object.Instantiate(Prefab("Player"), _origin + Vector3.up * .8f, Quaternion.identity);
        var selector = _player.GetComponent<PlayerDirectionalColliders>();
        selector.SetFacing(PlayerAimFacing.Down);
        _device = Object.Instantiate(Prefab("LureBeacon"), _origin, Quaternion.identity);
        var field = _device.GetComponent<PlayerFieldDevice>();
        var body = _device.GetComponentInChildren<PolygonCollider2D>();
        Physics2D.SyncTransforms();
        _player.transform.position += body.bounds.center - ActiveBody().bounds.center;
        field.Initialize(PlayerFieldDeviceKind.Beacon, _origin, _origin, .3f, 80f, 8f, 12f);
        var passage = _device.GetComponent<PlayerDeviceBodyCollision>();
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
        _device.GetComponent<PlayerDeviceSortingOrder>().Refresh();
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
        _device.GetComponent<PlayerDeviceSortingOrder>().Refresh();
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
        var layout = _hud.GetComponent<PlayerCombatHudSlideController>();
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

using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class PlayerDashRuntimeTests
{
    private GameObject _player;
    private GameObject _wall;
    private Keyboard _keyboard;
    private PlayerDash _dash;
    private PlayerMovement _movement;
    private Rigidbody2D _body;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _keyboard = InputSystem.AddDevice<Keyboard>();
        _player = new GameObject("Dash test player");
        _player.SetActive(false);
        _player.layer = LayerMask.NameToLayer("Player");
        _player.transform.position = new Vector3(14000, 14000);
        _body = _player.AddComponent<Rigidbody2D>();
        _body.gravityScale = 0;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _player.AddComponent<CircleCollider2D>().radius = .25f;
        _player.AddComponent<PlayerInputHandler>();
        _player.AddComponent<PlayerControlState>();
        _movement = _player.AddComponent<PlayerMovement>();
        _dash = _player.AddComponent<PlayerDash>();
        _player.SetActive(true);
        SetMove(true);
        yield return null;
    }

    private void SetMove(bool moving)
    {
        InputSystem.QueueStateEvent(_keyboard, moving ? new KeyboardState(Key.D) : new KeyboardState());
        InputSystem.Update();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1;
        if (_wall != null) Object.DestroyImmediate(_wall);
        if (_player != null) Object.DestroyImmediate(_player);
        if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DashBoostsMovementAndControlBlockStopsItWithoutCooldownBypass()
    {
        Assert.That(_dash.TryDash(), Is.True);
        Assert.That(_dash.TryDash(), Is.False);
        yield return new WaitForFixedUpdate();
        Assert.That(_body.linearVelocity.x, Is.GreaterThan(_movement.MoveSpeed));
        _player.GetComponent<PlayerControlState>().SetInputBlocked(true);
        yield return null;
        Assert.That(_dash.IsDashing, Is.False);
        Assert.That(_body.linearVelocity, Is.EqualTo(Vector2.zero));
        _player.GetComponent<PlayerControlState>().SetInputBlocked(false);
        Assert.That(_dash.TryDash(), Is.False);
        Assert.That(_dash.CooldownRemaining, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator StationaryDashKeepsLastDirectionAndPauseClearsVelocity()
    {
        SetMove(false);
        yield return null;
        Assert.That(_dash.TryDash(), Is.True);
        yield return new WaitForFixedUpdate();
        Assert.That(_body.linearVelocity.x, Is.GreaterThan(0));
        Assert.That(Mathf.Abs(_body.linearVelocity.y), Is.LessThan(.001f));
        Time.timeScale = 0;
        yield return null;
        Assert.That(_dash.IsDashing, Is.False);
        Assert.That(_body.linearVelocity, Is.EqualTo(Vector2.zero));
        Time.timeScale = 1;
        yield return new WaitForFixedUpdate();
        Assert.That(_body.linearVelocity, Is.EqualTo(Vector2.zero));
    }

    [UnityTest]
    public IEnumerator SolidWallStopsDashWithoutTunneling()
    {
        _wall = new GameObject("Thin dash obstacle", typeof(BoxCollider2D));
        _wall.transform.position = _player.transform.position + Vector3.right;
        var collider = _wall.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(.05f, 3);
        collider.includeLayers = 1 << _player.layer;
        collider.layerOverridePriority = 20;
        Physics2D.SyncTransforms();
        Assert.That(_dash.TryDash(), Is.True);
        for (int i = 0; i < 30 && _dash.IsDashing; i++) yield return new WaitForFixedUpdate();
        Assert.That(_dash.IsDashing, Is.False);
        Assert.That(_body.position.x, Is.LessThan(collider.bounds.min.x));
        Assert.That(_dash.CooldownRemaining, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator AuthoredAfterimageKeepsBodyAndWeaponAtTheirCapturedWorldPoses()
    {
        var source = new GameObject("Snapshot source", typeof(SpriteRenderer));
        var weaponObject = new GameObject("Snapshot source weapon", typeof(SpriteRenderer));
        weaponObject.transform.SetParent(source.transform, false);
        source.transform.localScale = new Vector3(.8f, .8f, 1);
        source.transform.position = _player.transform.position;
        weaponObject.transform.localPosition = new Vector3(.7f, -.3f);
        weaponObject.transform.localRotation = Quaternion.Euler(0, 0, 25);
        var body = source.GetComponent<SpriteRenderer>();
        body.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Player/Motions/V2/Idle/Idle_Right_0.png");
        var weapon = weaponObject.GetComponent<SpriteRenderer>();
        weapon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Player/Motions/V2/SeparatedWeapons/Idle/Idle_Right_0.png");
        weapon.flipX = true;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerDashAfterimage.prefab");
        var effect = Object.Instantiate(prefab, source.transform).GetComponent<PlayerDashAfterimage>();
        try
        {
            var data = new SerializedObject(effect);
            data.FindProperty("_lifetime").floatValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
            Vector3 bodyPosition = body.transform.position;
            Vector3 weaponPosition = weapon.transform.position;
            Vector3 bodyScale = body.transform.lossyScale;
            Quaternion weaponRotation = weapon.transform.rotation;
            effect.Capture(body, weapon);
            source.transform.position += Vector3.right * 2;
            yield return null;
            var copiedBody = effect.transform.Find("Body Snapshot").GetComponent<SpriteRenderer>();
            var copiedWeapon = effect.transform.Find("Weapon Snapshot").GetComponent<SpriteRenderer>();
            Assert.That(effect.IsPlaying, Is.True);
            Assert.That(copiedBody.sprite, Is.EqualTo(body.sprite));
            Assert.That(copiedWeapon.sprite, Is.EqualTo(weapon.sprite));
            Assert.That(copiedWeapon.flipX, Is.True);
            Assert.That(Vector3.Distance(copiedBody.transform.position, bodyPosition), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(copiedWeapon.transform.position, weaponPosition), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(copiedBody.transform.lossyScale, bodyScale), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(copiedWeapon.transform.rotation, weaponRotation), Is.LessThan(.001f));
            source.SetActive(false);
            Assert.That(effect.IsPlaying, Is.False);
        }
        finally { Object.DestroyImmediate(source); }
    }

    [Test]
    public void PlayerAndHudPrefabsHaveAuthoredDashReferences()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(prefab.GetComponent<PlayerDash>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<PlayerDashTrail>().IsConfigured, Is.True);
        Assert.That(prefab.GetComponentsInChildren<PlayerDashAfterimage>(true).Length, Is.EqualTo(4));
        Assert.That(prefab.GetComponent<Rigidbody2D>().collisionDetectionMode, Is.EqualTo(CollisionDetectionMode2D.Continuous));
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        Assert.That(hud.GetComponentInChildren<PlayerDashHud>(true).IsConfigured, Is.True);
        Assert.That(hud.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
    }
}

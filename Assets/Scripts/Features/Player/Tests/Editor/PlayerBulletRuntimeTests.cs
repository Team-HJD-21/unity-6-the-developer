using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerBulletRuntimeTests
{
    private GameObject _shooter;
    private GameObject _bulletPrefab;
    private Rigidbody2D _shooterBody;
    private PlayerAttack _attack;
    private Transform _muzzle;
    private readonly List<PlayerBullet> _bullets = new();
    private readonly List<GameObject> _targets = new();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerBullet.prefab");
        _shooter = new GameObject("Bullet speed test shooter");
        _shooter.SetActive(false);
        _shooter.transform.position = new Vector3(12000f, 12000f, 0f);
        _shooterBody = _shooter.AddComponent<Rigidbody2D>();
        _shooterBody.gravityScale = 0f;
        var input = _shooter.AddComponent<PlayerInputHandler>();
        input.enabled = false;

        var rigObject = new GameObject("Weapon Rig");
        rigObject.transform.SetParent(_shooter.transform, false);
        var rig = rigObject.AddComponent<PlayerWeaponRig>();
        var gun = new GameObject("Down Gun").transform;
        gun.SetParent(rig.transform, false);
        _muzzle = new GameObject("Down Muzzle").transform;
        _muzzle.SetParent(gun, false);
        _muzzle.localPosition = new Vector3(.4f, -.8f, 0f);
        SetReference(rig, "_playerRoot", _shooter.transform);
        SetReference(rig, "_downGun", gun);
        SetReference(rig, "_downMuzzle", _muzzle);

        var anchorObject = new GameObject("Muzzle Anchor");
        anchorObject.transform.SetParent(_shooter.transform, false);
        var anchor = anchorObject.AddComponent<PlayerMuzzleAnchor>();
        SetReference(anchor, "_playerRoot", _shooter.transform);
        SetReference(anchor, "_weaponRig", rig);
        _attack = _shooter.AddComponent<PlayerAttack>();
        SetReference(_attack, "_bulletPrefab", _bulletPrefab);
        _shooter.SetActive(true);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        ClearBullets();
        foreach (GameObject target in _targets)
            if (target != null) Object.DestroyImmediate(target);
        _targets.Clear();
        if (_shooter != null) Object.DestroyImmediate(_shooter);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator MovingShooter_PreservesConfiguredBulletSpeedAndFreeFlight()
    {
        float speed = new SerializedObject(_bulletPrefab.GetComponent<PlayerBullet>()).FindProperty("_speed").floatValue;
        var velocities = new[] { Vector2.zero, Vector2.right * 5f, Vector2.down * 5f,
            Vector2.up * 5f, new Vector2(3f, 4f), Vector2.right * 20f };

        foreach (Vector2 velocity in velocities)
        {
            _shooterBody.linearVelocity = velocity;
            Assert.That(_attack.TryFire(), Is.True);
            CaptureBullets();
            Assert.That(_bullets.Count, Is.EqualTo(5));
            var bullet = _bullets.OrderBy(candidate => Vector2.Angle(Vector2.down,
                candidate.GetComponent<Rigidbody2D>().linearVelocity)).First();
            var body = bullet.GetComponent<Rigidbody2D>();
            Assert.That(bullet.transform.parent, Is.Null);
            Assert.That(Vector3.Distance(bullet.transform.position, _muzzle.position), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(body.linearVelocity, Vector2.down * speed), Is.LessThan(.001f),
                $"Shooter velocity {velocity} must not affect bullet flight.");

            Vector2 startPosition = body.position;
            float startTime = Time.fixedTime;
            yield return new WaitForFixedUpdate();
            float elapsed = Time.fixedTime - startTime;
            Vector2 expectedPosition = startPosition + Vector2.down * speed * elapsed;
            Assert.That(Vector2.Distance(body.position, expectedPosition), Is.LessThan(.02f));
            Assert.That(body.linearVelocity.magnitude, Is.EqualTo(speed).Within(.001f));
            ClearBullets();
            yield return new WaitForSeconds(.21f);
        }
    }

    [UnityTest]
    public IEnumerator DefaultGun_FiresFivePelletsFromMuzzleAndRespectsFireInterval()
    {
        Assert.That(_attack.TryFire(), Is.True);
        Assert.That(_attack.TryFire(), Is.False);
        CaptureBullets();
        Assert.That(_bullets.Count, Is.EqualTo(5));
        float[] angles = _bullets.Select(bullet => Vector2.SignedAngle(Vector2.down,
            bullet.GetComponent<Rigidbody2D>().linearVelocity)).OrderBy(angle => angle).ToArray();
        foreach (PlayerBullet bullet in _bullets)
            Assert.That(Vector3.Distance(bullet.transform.position, _muzzle.position), Is.LessThan(.001f));
        for (int i = 0; i < angles.Length; i++)
            Assert.That(angles[i], Is.EqualTo(-12.5f + 6.25f * i).Within(.001f));
        ClearBullets();

        yield return new WaitForSeconds(.21f);
        Assert.That(_attack.TryFire(), Is.True);
        CaptureBullets();
        Assert.That(_bullets.Count, Is.EqualTo(5));
    }

    [UnityTest]
    public IEnumerator CryoBlaster_PreservesMuzzlePositionPelletCountAndSpread()
    {
        _attack.SelectNextWeapon();
        Assert.That(_attack.CurrentWeapon, Is.EqualTo(WeaponType.CryoBlaster));
        _shooterBody.linearVelocity = Vector2.right * 5f;
        Assert.That(_attack.TryFire(), Is.True);
        CaptureBullets();
        Assert.That(_bullets.Count, Is.EqualTo(5));
        var angles = _bullets.Select(bullet =>
            Vector2.SignedAngle(Vector2.down, bullet.GetComponent<Rigidbody2D>().linearVelocity)).OrderBy(value => value).ToArray();
        for (int i = 0; i < _bullets.Count; i++)
        {
            Assert.That(Vector3.Distance(_bullets[i].transform.position, _muzzle.position), Is.LessThan(.001f));
            Assert.That(angles[i], Is.EqualTo(-12.5f + 6.25f * i).Within(.001f));
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator InvalidDirection_IsRejectedWithoutChangingVelocity()
    {
        var bullet = Object.Instantiate(_bulletPrefab, _muzzle.position, Quaternion.identity).GetComponent<PlayerBullet>();
        _bullets.Add(bullet);
        foreach (Vector2 direction in new[] { Vector2.zero, new Vector2(float.NaN, 1f), new Vector2(1f, float.PositiveInfinity) })
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[PlayerBullet\]"));
            Assert.That(bullet.Initialize(Color.white, 13f, direction), Is.False);
            Assert.That(bullet.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator RepeatedHits_DoNotPushDynamicTarget()
    {
        Rigidbody2D target = CreateTarget("Enemy");
        Vector2 startPosition = target.position;
        Assert.That(_bulletPrefab.GetComponent<Collider2D>().isTrigger, Is.True);
        for (int shot = 0; shot < 5; shot++)
        {
            PlayerBullet bullet = SpawnBullet(1f);
            for (int step = 0; step < 20 && bullet != null; step++) yield return new WaitForFixedUpdate();
            Assert.That(bullet == null, Is.True, "The projectile must register its hit before lifetime expiry.");
            Assert.That(target.linearVelocity.sqrMagnitude, Is.LessThan(.000001f));
            Assert.That(Vector2.Distance(target.position, startPosition), Is.LessThan(.002f));
        }
    }

    [UnityTest]
    public IEnumerator PlayerChildCollider_IsIgnoredAndStaticObstacleStopsBullet()
    {
        Rigidbody2D target = CreateTarget("Player");
        PlayerBullet bullet = SpawnBullet(1f);
        for (int step = 0; step < 10; step++) yield return new WaitForFixedUpdate();
        Assert.That(bullet != null, Is.True, "An untagged child collider must resolve to its Player Rigidbody owner.");
        Assert.That(target.linearVelocity.sqrMagnitude, Is.LessThan(.000001f));
        ClearBullets();

        target.gameObject.tag = "Untagged";
        target.bodyType = RigidbodyType2D.Static;
        bullet = SpawnBullet(1f);
        for (int step = 0; step < 20 && bullet != null; step++) yield return new WaitForFixedUpdate();
        Assert.That(bullet == null, Is.True, "A trigger projectile must still stop at an obstacle.");
    }

    [UnityTest]
    [Explicit("Initial_Stage의 실제 NGO Host에서 서버 적 피해를 확인할 때 실행")]
    public IEnumerator ServerEnemy_TakesDamageOnceFromTriggerProjectile()
    {
        EnemyHealth health = null;
        float deadline = Time.realtimeSinceStartup + 20f;
        while (health == null && Time.realtimeSinceStartup < deadline)
        {
            health = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.IsServer && candidate.IsSpawned && candidate.CurrentHealth > 1);
            yield return null;
        }
        Assert.That(health, Is.Not.Null, "Initial_Stage의 실제 서버 스폰 적이 필요합니다.");
        int initialHealth = health.CurrentHealth;
        health.transform.position = _muzzle.position + Vector3.down * 1.2f;
        Physics2D.SyncTransforms();
        PlayerBullet bullet = SpawnBullet(1f);
        for (int step = 0; step < 20 && bullet != null; step++) yield return new WaitForFixedUpdate();
        Assert.That(bullet == null, Is.True);
        Assert.That(health.CurrentHealth, Is.EqualTo(initialHealth - 1));
    }

    private Rigidbody2D CreateTarget(string tag)
    {
        var target = new GameObject("Bullet hit test target");
        _targets.Add(target);
        target.tag = tag;
        target.layer = LayerMask.NameToLayer("Enemy");
        target.transform.position = _muzzle.position + Vector3.down * 1.2f;
        var body = target.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        var hitbox = new GameObject("Untagged child hitbox");
        hitbox.layer = target.layer;
        hitbox.transform.SetParent(target.transform, false);
        hitbox.AddComponent<CircleCollider2D>().radius = .6f;
        return body;
    }

    private PlayerBullet SpawnBullet(float damage)
    {
        var bullet = Object.Instantiate(_bulletPrefab, _muzzle.position, Quaternion.identity).GetComponent<PlayerBullet>();
        _bullets.Add(bullet);
        Assert.That(bullet.Initialize(Color.white, damage, Vector2.down), Is.True);
        return bullet;
    }

    private void CaptureBullets()
    {
        _bullets.AddRange(Object.FindObjectsByType<PlayerBullet>(FindObjectsSortMode.None)
            .Where(bullet => Vector3.Distance(bullet.transform.position, _shooter.transform.position) < 5f));
    }

    private void ClearBullets()
    {
        foreach (PlayerBullet bullet in _bullets)
            if (bullet != null) Object.DestroyImmediate(bullet.gameObject);
        _bullets.Clear();
    }

    private static void SetReference(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}

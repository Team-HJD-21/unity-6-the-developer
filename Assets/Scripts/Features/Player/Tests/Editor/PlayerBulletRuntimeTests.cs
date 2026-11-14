using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using TeamHJD.Game.Infrastructure;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

public sealed class PlayerBulletRuntimeTests
{
    private GameObject _shooter;
    private GameObject _bulletPrefab;
    private Rigidbody2D _shooterBody;
    private PlayerAttack _attack;
    private PlayerWeaponSelection _selection;
    private Transform _muzzle;
    private readonly List<PlayerBullet> _bullets = new();
    private readonly List<GameObject> _targets = new();
    private readonly List<Object> _runtimeAssets = new();
    private readonly List<Scene> _testScenes = new();
    private float _previousMaximumDelta, _previousTimeScale;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _previousMaximumDelta = Time.maximumDeltaTime; _previousTimeScale = Time.timeScale;
        Time.maximumDeltaTime = .02f; Time.timeScale = 1;
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
        _selection = _shooter.AddComponent<PlayerWeaponSelection>();
        SetReference(_selection, "_bulletPrefab", _bulletPrefab);
        SetReference(_selection, "_sniperBulletPrefab",
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/SniperBullet.prefab"));
        var spray = _shooter.AddComponent<PlayerCryoSpray>();
        SetReference(spray, "_vfxPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/CryoSprayVfx.prefab"));
        _attack = _shooter.AddComponent<PlayerAttack>();
        _shooter.SetActive(true);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        ClearBullets();
        ClearImpactEffects();
        foreach (GameObject target in _targets)
            if (target != null) Object.DestroyImmediate(target);
        _targets.Clear();
        foreach (Object asset in _runtimeAssets)
            if (asset != null) Object.DestroyImmediate(asset);
        _runtimeAssets.Clear();
        foreach (Scene scene in _testScenes)
        {
            if (!scene.IsValid() || !scene.isLoaded) continue;
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) while (!unload.isDone) yield return null;
        }
        _testScenes.Clear();
        if (_shooter != null) Object.DestroyImmediate(_shooter);
        Time.maximumDeltaTime = _previousMaximumDelta; Time.timeScale = _previousTimeScale;
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
            Assert.That(_bullets.Count, Is.EqualTo(1));
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
            yield return WaitGameTime(.21f);
        }
    }

    [UnityTest]
    public IEnumerator DefaultGun_FiresOneRoundFromMuzzleAndRespectsFireInterval()
    {
        Assert.That(_attack.TryFire(), Is.True);
        Assert.That(_attack.TryFire(), Is.False);
        CaptureBullets();
        Assert.That(_bullets.Count, Is.EqualTo(1));
        float[] angles = _bullets.Select(bullet => Vector2.SignedAngle(Vector2.down,
            bullet.GetComponent<Rigidbody2D>().linearVelocity)).OrderBy(angle => angle).ToArray();
        foreach (PlayerBullet bullet in _bullets)
            Assert.That(Vector3.Distance(bullet.transform.position, _muzzle.position), Is.LessThan(.001f));
        for (int i = 0; i < angles.Length; i++)
            Assert.That(angles[i], Is.EqualTo(0).Within(.001f));
        ClearBullets();

        yield return WaitGameTime(.21f);
        Assert.That(_attack.TryFire(), Is.True);
        CaptureBullets();
        Assert.That(_bullets.Count, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator CryoBlaster_UsesAreaDamageWithoutCreatingBullets()
    {
        Assert.That(_selection.TrySelectWeapon(WeaponType.CryoBlaster), Is.True);
        var enemy = CreateEnemy(1);
        Assert.That(_attack.TryFire(), Is.True);
        CaptureBullets();
        Assert.That(_bullets, Is.Empty);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.MaxHealth - 8));
        Assert.That(_attack.TryGetCurrentWeaponStats(out var stats), Is.True);
        Assert.That(stats.IsSpray, Is.True);
        Assert.That(stats.ProjectileCount, Is.Zero);
        Assert.That(stats.Range, Is.EqualTo(4.5f).Within(.001f));
        Assert.That(stats.Interval, Is.EqualTo(.1f).Within(.001f));
        Assert.That(_attack.TryFire(), Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator Cryo_MovingForwardAndBackwardKeepsEmittingAcrossNativeFrames()
    {
        _attack.enabled = false;
        var spray = _shooter.GetComponent<PlayerCryoSpray>();
        using var random = new TeamHJD.Game.Application.RandomService(17);
        spray.BindPresentationRandom(random);
        var particlesField = typeof(PlayerCryoSpray).GetField("_particles", BindingFlags.Instance | BindingFlags.NonPublic);
        float moveSpeed = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab")
            .GetComponent<PlayerMovement>().BaseMoveSpeed;
        foreach (float sign in new[] { -1f, 1f })
        {
            spray.Stop(true);
            _shooterBody.linearVelocity = Vector2.down * sign * moveSpeed;
            for (int frame = 0; frame < 20; frame++)
            {
                spray.Show(_muzzle.position, Vector2.down);
                yield return new WaitForFixedUpdate();
                yield return null;
                if (frame < 5) continue;
                var systems = (ParticleSystem[])particlesField.GetValue(spray);
                Assert.That(spray.IsEmitting, Is.True);
                Assert.That(systems.Sum(ps => ps.particleCount), Is.GreaterThan(0), $"movement={sign}, frame={frame}");
            }
        }
    }

    [UnityTest]
    public IEnumerator Sniper_FiresOneFastRoundWithoutSpreadAndRespectsCooldown()
    {
        _selection.SelectNextWeapon();
        _selection.SelectNextWeapon();
        Assert.That(_selection.CurrentWeapon, Is.EqualTo(WeaponType.Sniper));
        _shooterBody.linearVelocity = Vector2.right * 5f;
        Assert.That(_attack.TryFire(), Is.True);
        Assert.That(_attack.TryFire(), Is.False);
        CaptureBullets();
        Assert.That(_bullets.Count, Is.EqualTo(1));
        var bullet = _bullets.Single();
        Assert.That(Vector3.Distance(bullet.transform.position, _muzzle.position), Is.LessThan(.001f));
        Assert.That(Vector2.Distance(bullet.GetComponent<Rigidbody2D>().linearVelocity, Vector2.down * 70f), Is.LessThan(.001f));
        Assert.That(new SerializedObject(bullet).FindProperty("_damage").floatValue, Is.EqualTo(45f));
        yield return WaitGameTime(.25f);
        Assert.That(_attack.TryFire(), Is.False, "The slower sniper interval must not reuse the default gun interval.");
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
        target.gameObject.layer = LayerMask.NameToLayer("Default");
        target.GetComponentInChildren<Collider2D>().gameObject.layer = LayerMask.NameToLayer("Default");
        target.bodyType = RigidbodyType2D.Static;
        bullet = SpawnBullet(1f);
        for (int step = 0; step < 20 && bullet != null; step++) yield return new WaitForFixedUpdate();
        Assert.That(bullet == null, Is.True, "A trigger projectile must still stop at an obstacle.");
    }

    [TestCase("Assets/Prefabs/Player/PlayerBullet.prefab")]
    [TestCase("Assets/Prefabs/Player/SniperBullet.prefab")]
    public void BulletPrefab_IncludesWorldBodyLayersWithoutChangingGlobalCollisionMatrix(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var hitbox = prefab.GetComponent<Collider2D>();
        foreach (string layer in new[] { "Default", "CU", "Turret", "Forest" })
            Assert.That(hitbox.includeLayers.value & (1 << LayerMask.NameToLayer(layer)), Is.Not.Zero, layer);
        Assert.That(hitbox.includeLayers.value & (1 << LayerMask.NameToLayer("Ground_1")), Is.Zero);
        Assert.That(hitbox.isTrigger, Is.True, "Bullets must not push bodies through contact impulses.");
    }

    [UnityTest]
    public IEnumerator ThinWorldBodies_StopEveryGunAndPlayOneExistingImpact()
    {
        foreach (var variant in new[] { (name: "PlayerBullet", cryogenic: false),
            (name: "PlayerBullet", cryogenic: true), (name: "SniperBullet", cryogenic: false) })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Player/{variant.name}.prefab");
            var expectedImpact = new SerializedObject(prefab.GetComponent<PlayerProjectileView>())
                .FindProperty("_impactPrefab").objectReferenceValue;
            foreach (string layer in new[] { "Default", "CU", "Turret", "Forest", "PlayerFieldDevice" })
            {
                var obstacle = CreateWorldObstacle(layer, "Untagged", false);
                // 중복 접촉도 명중 이펙트 한 번으로 합쳐져야 합니다.
                obstacle.AddComponent<BoxCollider2D>().size = new Vector2(1f, .02f);
                Physics2D.SyncTransforms();
                var bullet = Object.Instantiate(prefab, _muzzle.position, Quaternion.identity).GetComponent<PlayerBullet>();
                _bullets.Add(bullet);
                Assert.That(bullet.Initialize(Color.cyan, 1f, Vector2.down, variant.cryogenic), Is.True);
                for (int step = 0; step < 8 && bullet != null; step++) yield return new WaitForFixedUpdate();
                yield return null;

                Assert.That(bullet == null, Is.True, $"{variant} must stop at the thin {layer} body before lifetime expiry.");
                var effects = FindImpactEffects();
                Assert.That(effects.Length, Is.EqualTo(1), $"{variant}: one impact for the first blocking body.");
                Assert.That(effects[0].name, Is.EqualTo(expectedImpact.name + "(Clone)"),
                    "World and monster hits must use the same authored impact prefab.");
                Assert.That(Vector2.Distance(effects[0].transform.position, _muzzle.position), Is.LessThan(.6f),
                    "The projectile must stop at the near surface rather than the next physics position.");
                Object.DestroyImmediate(obstacle);
                ClearImpactEffects();
            }
        }
    }

    [UnityTest]
    public IEnumerator GroundPlayerProjectilesAndDetectionTriggers_DoNotConsumeBullet()
    {
        CreateWorldObstacle("Default", "Ground", false);
        CreateWorldObstacle("Default", "Player", false);
        CreateWorldObstacle("Default", "Bullet", false);
        CreateWorldObstacle("Turret", "Untagged", true);
        CreateWorldObstacle("PocInteractArea", "Untagged", true);
        Physics2D.SyncTransforms();
        PlayerBullet bullet = SpawnBullet(1f);
        for (int step = 0; step < 8; step++) yield return new WaitForFixedUpdate();
        Assert.That(bullet != null, Is.True, "Only enemy hitboxes and physical world bodies should consume bullets.");
        Assert.That(FindImpactEffects(), Is.Empty);
    }

    [UnityTest]
    public IEnumerator EnemyTriggerHitbox_StillConsumesBulletAndPlaysImpact()
    {
        CreateWorldObstacle("Enemy", "Enemy", true);
        Physics2D.SyncTransforms();
        PlayerBullet bullet = SpawnBullet(1f);
        for (int step = 0; step < 8 && bullet != null; step++) yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(bullet == null, Is.True);
        Assert.That(FindImpactEffects().Length, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator BarrierMembrane_PassesEveryGunWithoutImpact()
    {
        foreach (var variant in new[] { (name: "PlayerBullet", cryogenic: false),
            (name: "PlayerBullet", cryogenic: true), (name: "SniperBullet", cryogenic: false) })
        {
            var devicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/EnergyBarrier.prefab");
            Vector3 center = _muzzle.position + Vector3.down;
            var device = Object.Instantiate(devicePrefab);
            _targets.Add(device);
            var field = device.GetComponent<PlayerFieldDevice>();
            field.Initialize(PlayerFieldDeviceKind.Barrier, center + Vector3.left * 3f,
                center + Vector3.right * 3f, .3f, 100f, 20f);
            var membrane = device.GetComponent<BoxCollider2D>();
            var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Player/{variant.name}.prefab");
            var bullet = Object.Instantiate(bulletPrefab, _muzzle.position, Quaternion.identity).GetComponent<PlayerBullet>();
            _bullets.Add(bullet);
            Physics2D.SyncTransforms();
            Assert.That(bullet.Initialize(Color.white, 13f, Vector2.down, variant.cryogenic), Is.True);
            // 접촉 콜백과 빠른 탄환의 경로 Cast가 모두 같은 통과 조건을 사용해야 합니다.
            bullet.SendMessage("OnTriggerEnter2D", membrane);
            for (int step = 0; step < 8 && bullet != null; step++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(bullet != null, Is.True, variant.name + " must pass the membrane.");
            Assert.That(bullet.transform.position.y, Is.LessThan(membrane.bounds.min.y - .2f));
            Assert.That(FindImpactEffects().Length, Is.Zero);
            Assert.That(field.Health, Is.EqualTo(100f));
            ClearBullets();
            Object.DestroyImmediate(device);
        }
    }

    [UnityTest]
    public IEnumerator AuthoredDeviceBodies_StopBulletsDespiteTheirContactExclusions()
    {
        foreach (string name in new[] { "PlayerTurret", "LureBeacon", "EnergyBarrier", "bomb" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Player/{name}.prefab");
            Vector3 position = _muzzle.position + Vector3.down * 3f;
            var device = Object.Instantiate(prefab, position, Quaternion.identity);
            _targets.Add(device);
            if (device.TryGetComponent(out PlayerFieldDevice fieldDevice))
            {
                bool barrier = name == "EnergyBarrier";
                fieldDevice.Initialize(barrier ? PlayerFieldDeviceKind.Barrier : PlayerFieldDeviceKind.Beacon,
                    position + (barrier ? Vector3.left * 2f : Vector3.zero),
                    position + (barrier ? Vector3.right * 2f : Vector3.zero), .3f, 100f, 20f, 1f);
            }
            Physics2D.SyncTransforms();
            var body = device.GetComponentsInChildren<Collider2D>()
                .First(collider => collider.isActiveAndEnabled && !collider.isTrigger &&
                    (fieldDevice == null || !fieldDevice.IsBarrierMembrane(collider)));
            int bulletLayer = 1 << _bulletPrefab.layer;
            Assert.That(body.excludeLayers.value & bulletLayer, Is.Not.Zero, name + " excludes physical bullet contact.");
            Assert.That(body.layerOverridePriority, Is.GreaterThan(_bulletPrefab.GetComponent<Collider2D>().layerOverridePriority));
            Assert.That(device.transform.parent, Is.Null, "Installed devices must not inherit the Player tag.");
            float initialHealth = fieldDevice != null ? fieldDevice.Health : 0f;
            var turretHealth = device.GetComponent<PlayerTurretHealth>();
            float initialTurretHealth = turretHealth != null ? turretHealth.Health : 0f;
            Vector3 launch = new(body.bounds.center.x, body.bounds.max.y + .55f, 0f);
            var bullet = Object.Instantiate(_bulletPrefab, launch, Quaternion.identity).GetComponent<PlayerBullet>();
            _bullets.Add(bullet);
            Assert.That(bullet.Initialize(Color.white, 13f, Vector2.down), Is.True);
            for (int step = 0; step < 8 && bullet != null; step++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(bullet == null, Is.True, name + " must be found by the projectile query, even without trigger contacts.");
            Assert.That(FindImpactEffects().Length, Is.EqualTo(1), name);
            if (fieldDevice != null) Assert.That(fieldDevice.Health, Is.EqualTo(initialHealth));
            if (turretHealth != null) Assert.That(turretHealth.Health, Is.EqualTo(initialTurretHealth));
            Object.DestroyImmediate(device);
            ClearImpactEffects();
        }
    }

    [UnityTest]
    public IEnumerator DefaultUntaggedGround_IsIgnoredButEnvironmentTilemapBlocksBullet()
    {
        Scene scene = SceneManager.CreateScene("Projectile isolated ground test");
        _testScenes.Add(scene);
        var grid = new GameObject("Projectile ground test grid");
        _targets.Add(grid);
        SceneManager.MoveGameObjectToScene(grid, scene);
        grid.AddComponent<Grid>();
        grid.transform.position = _muzzle.position + new Vector3(-.5f, -.55f, 0f);
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.colliderType = Tile.ColliderType.Grid;
        _runtimeAssets.Add(tile);
        Tilemap ground = CreateTestTilemap("Ground on Default with no tag", grid.transform);
        for (int x = -4; x <= 4; x++)
        for (int y = -4; y <= 4; y++) ground.SetTile(new Vector3Int(x, y, 0), tile);
        ground.CompressBounds();
        ground.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        Physics2D.SyncTransforms();
        PlayerBullet freeBullet = SpawnBullet(1f);
        SceneManager.MoveGameObjectToScene(freeBullet.gameObject, scene);
        for (int step = 0; step < 3; step++) yield return new WaitForFixedUpdate();
        Assert.That(freeBullet != null, Is.True, "An untagged Default-layer ground must not consume bullets inside it.");
        Assert.That(FindImpactEffects(), Is.Empty);
        ClearBullets();

        Tilemap environment = CreateTestTilemap("Environment obstacle tilemap", grid.transform);
        environment.SetTile(new Vector3Int(0, -1, 0), tile);
        environment.CompressBounds();
        environment.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        Physics2D.SyncTransforms();
        PlayerBullet blockedBullet = SpawnBullet(1f);
        SceneManager.MoveGameObjectToScene(blockedBullet.gameObject, scene);
        for (int step = 0; step < 8 && blockedBullet != null; step++) yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(blockedBullet == null, Is.True, "Excluding scene ground must preserve separate obstacle tilemap hits.");
        Assert.That(FindImpactEffects().Length, Is.EqualTo(1));
    }

    [UnityTest]
    [Explicit("Initial_Stage의 실제 NGO Host에서 서버 적 피해를 확인할 때 실행")]
    public IEnumerator ServerEnemy_TakesDamageOnceFromTriggerProjectile()
    {
        const string scenePath = "Assets/Scenes/Gameplay/Initial_Stage.unity";
        // CLI의 빈 씬에서도 서버 적을 생성하고 SetUp의 탄환·발사대는 유지합니다.
        if (!SceneManager.GetSceneByPath(scenePath).isLoaded)
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);

        EnemyHealth health = null;
        float deadline = Time.realtimeSinceStartup + 20f;
        while (health == null && Time.realtimeSinceStartup < deadline)
        {
            health = Object.FindObjectsByType<EnemyNetworkController>(FindObjectsSortMode.None)
                .Where(driver => driver.IsServer && driver.IsSpawned)
                .Select(driver => driver.GetComponent<EnemyHealth>())
                .FirstOrDefault(candidate => candidate != null && candidate.CurrentHealth > 1);
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

    [UnityTest]
    public IEnumerator Sniper_PiercesDistinctEnemiesOnceButStopsBeforeEnemiesBehindAWall()
    {
        var first = CreateEnemy(.45f);
        var second = CreateEnemy(1.1f);
        var hidden = CreateEnemy(2.3f);
        var sensor = new GameObject("Large detection sensor"); _targets.Add(sensor);
        sensor.tag = "Enemy"; sensor.layer = LayerMask.NameToLayer("EnemySensor"); sensor.transform.position = _muzzle.position;
        var trigger = sensor.AddComponent<CircleCollider2D>(); trigger.isTrigger = true; trigger.radius = 3;
        var wall = CreateWorldObstacle("Forest", "Untagged", false);
        wall.transform.position = _muzzle.position + Vector3.down * 1.8f;
        var bullet = CreateSniper();
        Physics2D.SyncTransforms();
        for (int step = 0; step < 8 && bullet != null; step++) yield return new WaitForFixedUpdate();
        Assert.That(bullet == null, Is.True, "A forest body must stop a penetrating round.");
        Assert.That(first.CurrentHealth, Is.EqualTo(first.MaxHealth - 1));
        Assert.That(second.CurrentHealth, Is.EqualTo(second.MaxHealth - 1));
        Assert.That(hidden.CurrentHealth, Is.EqualTo(hidden.MaxHealth));
        yield return null;
        Assert.That(FindImpactEffects().Length, Is.EqualTo(3), "Two enemy impacts and one wall impact; no sensor/duplicate impact.");
    }

    [UnityTest]
    public IEnumerator Sniper_CheckpointRoundTripPreservesPiercedHealthIdentities()
    {
        var enemy = CreateEnemy(1);
        var hitbox = enemy.GetComponentInChildren<Collider2D>();
        var bullet = CreateSniper();
        CallPrivate(bullet, "ProcessHit", hitbox); CallPrivate(bullet, "ProcessHit", hitbox);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.MaxHealth - 1));
        object state = CallPrivate(bullet, "CaptureCheckpoint");
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            state.GetType().GetMethod("Write").Invoke(state, new object[] { writer });
        stream.Position = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
        var saved = state.GetType().GetMethod("Read", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { reader, 3, new BinaryCheckpointPayloadCodec() });
        Object.DestroyImmediate(bullet.gameObject);
        var restored = CreateSniper(); CallPrivate(restored, "RestoreCheckpoint", saved);
        CallPrivate(restored, "ProcessHit", hitbox);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.MaxHealth - 1), "Loading must not deal damage twice to the same logical enemy.");
        var other = CreateEnemy(2);
        CallPrivate(restored, "ProcessHit", other.GetComponentInChildren<Collider2D>());
        Assert.That(other.CurrentHealth, Is.EqualTo(other.MaxHealth - 1));
        yield break;
    }

    [UnityTest]
    public IEnumerator LegacyCryoCheckpointLoadsWithoutHistoryAndPreservesFlight()
    {
        var bullet = Object.Instantiate(_bulletPrefab, _muzzle.position, Quaternion.identity).GetComponent<PlayerBullet>(); _bullets.Add(bullet);
        Assert.That(bullet.Initialize(Color.cyan, 1, Vector2.down, true), Is.True);
        bullet.GetComponent<Rigidbody2D>().linearVelocity = Vector2.down * 45;
        typeof(PlayerBullet).GetField("_expiresAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bullet, Time.timeAsDouble + 1.3);
        object state = CallPrivate(bullet, "CaptureCheckpoint");
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            state.GetType().GetMethod("Write").Invoke(state, new object[] { writer });
        stream.SetLength(stream.Length - sizeof(int)); // v1/v2 has no trailing hit-history count.
        stream.Position = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
        var saved = state.GetType().GetMethod("Read", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { reader, 2, new BinaryCheckpointPayloadCodec() });
        Assert.That(stream.Position, Is.EqualTo(stream.Length));
        CallPrivate(bullet, "RestoreCheckpoint", saved);
        Assert.That(bullet.GetComponent<Rigidbody2D>().linearVelocity.magnitude, Is.EqualTo(45).Within(.001f));
        Assert.That((double)typeof(PlayerBullet).GetField("_expiresAt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bullet),
            Is.EqualTo(Time.timeAsDouble + 1.3).Within(.001));
        yield break;
    }

    [UnityTest]
    public IEnumerator Cryo_EmissionsConsumeOneRoundAndStopForAutomaticReload()
    {
        var clock = new WeaponTestClock { ElapsedSeconds = Time.timeAsDouble + 10 };
        TeamHJD.Game.Presentation.Scene.FieldSimulationActor.Attach(_shooter, clock);
        _shooter.GetComponent<PlayerInputHandler>().enabled = true;
        var ammo = _shooter.AddComponent<PlayerWeaponAmmo>();
        typeof(PlayerAttack).GetField("_ammo", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_attack, ammo);
        Assert.That(_selection.TrySelectWeapon(WeaponType.CryoBlaster), Is.True);
        for (int emitted = 0; emitted < 15; emitted++)
        {
            Assert.That(_attack.TryFire(), Is.True, "Emission " + emitted);
            Assert.That(ammo.CurrentRounds, Is.EqualTo(14 - emitted));
            ClearLiveBullets();
            clock.ElapsedSeconds += .11;
        }
        Assert.That(ammo.IsReloading, Is.True);
        Assert.That(_attack.TryFire(), Is.False);
        clock.ElapsedSeconds += .62;
        CallPrivate(ammo, "Update");
        Assert.That(ammo.IsReloading, Is.True, "Reload must still be in progress at 0.73 seconds.");
        clock.ElapsedSeconds += .03;
        CallPrivate(ammo, "Update");
        Assert.That(ammo.IsReloading, Is.False);
        Assert.That(_attack.TryFire(), Is.True);
        ClearLiveBullets();
        yield break;
    }

    private sealed class WeaponTestClock : TeamHJD.Game.Contracts.IMatchTime
    {
        public double ElapsedSeconds { get; set; }
        public bool IsAvailable => true;
        public TeamHJD.Game.Domain.MatchTimeSnapshot Snapshot { get; } = new(
            new TeamHJD.Game.Domain.MatchId("cryo-reload-test"), System.Guid.NewGuid(), 0, 0, 0, 1, false, true);
    }

    private void ClearLiveBullets() { CaptureBullets(); ClearBullets(); }
    private static IEnumerator WaitGameTime(float seconds)
    {
        double end = Time.timeAsDouble + seconds;
        double deadline = Time.realtimeSinceStartupAsDouble + 20;
        while (Time.timeAsDouble < end && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Assert.That(Time.timeAsDouble, Is.GreaterThanOrEqualTo(end), "The Play Mode clock did not advance.");
    }
    private static object CallPrivate(object source, string method, params object[] args) => source.GetType()
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(source, args);
    private PlayerBullet CreateSniper()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/SniperBullet.prefab");
        var bullet = Object.Instantiate(prefab, _muzzle.position, Quaternion.identity).GetComponent<PlayerBullet>(); _bullets.Add(bullet);
        Assert.That(bullet.Initialize(Color.white, 1, Vector2.down), Is.True);
        return bullet;
    }
    private EnemyHealth CreateEnemy(float distance)
    {
        var body = CreateTarget("Enemy"); body.bodyType = RigidbodyType2D.Kinematic;
        body.transform.position = _muzzle.position + Vector3.down * distance;
        var hitbox = body.GetComponentInChildren<CircleCollider2D>(); hitbox.radius = .1f; hitbox.isTrigger = true;
        // Two shapes owned by the same EnemyHealth must not cause two penetrating hits.
        var duplicate = hitbox.gameObject.AddComponent<CircleCollider2D>(); duplicate.radius = .09f; duplicate.isTrigger = true;
        var health = body.gameObject.AddComponent<EnemyHealth>(); health.Initialize();
        return health;
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
        hitbox.layer = tag == "Enemy" ? LayerMask.NameToLayer("EnemyHitBox") : target.layer;
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

    private GameObject CreateWorldObstacle(string layer, string tag, bool trigger)
    {
        var obstacle = new GameObject($"Bullet obstacle {layer} {tag}");
        _targets.Add(obstacle);
        obstacle.layer = LayerMask.NameToLayer(tag == "Enemy" ? "EnemyHitBox" : layer);
        obstacle.tag = tag;
        obstacle.transform.position = _muzzle.position + Vector3.down * .55f;
        var hitbox = obstacle.AddComponent<BoxCollider2D>();
        hitbox.size = new Vector2(1f, .02f);
        hitbox.isTrigger = trigger;
        return obstacle;
    }

    private static Tilemap CreateTestTilemap(string name, Transform parent)
    {
        var mapObject = new GameObject(name);
        mapObject.transform.SetParent(parent, false);
        var map = mapObject.AddComponent<Tilemap>();
        mapObject.AddComponent<TilemapCollider2D>();
        return map;
    }

    private PlayerSpriteEffect[] FindImpactEffects() =>
        Object.FindObjectsByType<PlayerSpriteEffect>(FindObjectsSortMode.None)
            .Where(effect => Vector3.Distance(effect.transform.position, _muzzle.position) < 20f).ToArray();

    private void ClearImpactEffects()
    {
        if (_muzzle == null) return;
        foreach (PlayerSpriteEffect effect in FindImpactEffects()) Object.DestroyImmediate(effect.gameObject);
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

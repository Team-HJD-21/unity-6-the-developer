using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

public sealed class PlayerTurretPlacementTests
{
    private Scene _scene;
    private GameObject _player;
    private GameObject _prefab;
    private Tilemap _ground;
    private Tile _tile;
    private Vector3 _origin = new Vector3(12000f, 12000f, 0f);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _scene = SceneManager.CreateScene("Turret placement test");
        _prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/TemporaryTurret.prefab");
        _player = CreateObject("Player");
        _player.transform.position = _origin;
        _player.layer = LayerMask.NameToLayer("Player");

        GameObject grid = CreateObject("Ground grid");
        grid.transform.position = _origin;
        grid.AddComponent<Grid>();
        GameObject map = CreateObject("Ground");
        map.transform.SetParent(grid.transform, false);
        _ground = map.AddComponent<Tilemap>();
        map.AddComponent<TilemapCollider2D>();
        _tile = ScriptableObject.CreateInstance<Tile>();
        _tile.colliderType = Tile.ColliderType.None;
        for (int y = -10; y <= 10; y++)
        for (int x = -10; x <= 10; x++)
            _ground.SetTile(new Vector3Int(x, y, 0), _tile);
        Physics2D.SyncTransforms();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
        if (_tile != null) Object.DestroyImmediate(_tile);
        yield return new ExitPlayMode();
    }

    [Test]
    public void FootprintOutsideGroundOrOverHole_IsRejected()
    {
        Assert.That(CanPlace(_origin), Is.True);
        Assert.That(CanPlace(_origin + new Vector3(10.9f, 0f, 0f)), Is.False,
            "A center inside the last cell must not allow the base to hang outside.");
        Assert.That(Evaluate(_origin + new Vector3(10.9f, 0f, 0f)), Is.EqualTo(PlayerTurretPlacementResult.OutsideGround));
        _ground.SetTile(Vector3Int.zero, null);
        Assert.That(CanPlace(_origin), Is.False);
        Assert.That(PlayerTurretPlacementRules.CanPlace(_prefab, _origin, _player, null), Is.False);
        Assert.That(PlayerTurretPlacementRules.Evaluate(_prefab, _origin, _player, null),
            Is.EqualTo(PlayerTurretPlacementResult.GroundUnavailable));
    }

    [Test]
    public void ExistingTurretBase_BlocksPlacementDespiteDifferentSortingZ()
    {
        GameObject existing = Object.Instantiate(_prefab, _origin + Vector3.forward * 50f, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(existing, _scene);
        existing.GetComponent<TemporaryTurret>().enabled = false;
        Physics2D.SyncTransforms();
        Assert.That(CanPlace(_origin), Is.False);
        Assert.That(Evaluate(_origin), Is.EqualTo(PlayerTurretPlacementResult.OverlapsTurret));
        Assert.That(CanPlace(_origin + Vector3.right * 5f), Is.True);
    }

    [Test]
    public void WallBlocksPlacement_ButTriggerDoesNot()
    {
        GameObject obstacle = CreateObject("Wall");
        obstacle.transform.position = _origin;
        var collider = obstacle.AddComponent<BoxCollider2D>();
        Physics2D.SyncTransforms();
        Assert.That(CanPlace(_origin), Is.False);
        Assert.That(Evaluate(_origin), Is.EqualTo(PlayerTurretPlacementResult.BlockedByObstacle));
        collider.isTrigger = true;
        Assert.That(CanPlace(_origin), Is.True);
    }

    [Test]
    public void InstallerBodyBlocksPlacement_ButBulletHitboxDoesNot()
    {
        GameObject body = CreateObject("Installer body");
        body.transform.SetParent(_player.transform, false);
        var collider = body.AddComponent<BoxCollider2D>();
        Physics2D.SyncTransforms();
        Assert.That(Evaluate(_origin), Is.EqualTo(PlayerTurretPlacementResult.OverlapsPlayer));
        collider.isTrigger = true;
        Assert.That(CanPlace(_origin), Is.True);
    }

    [TestCase("Player", PlayerTurretPlacementResult.OverlapsPlayer)]
    [TestCase("Enemy", PlayerTurretPlacementResult.OverlapsEnemy)]
    public void ActorChildBodyBlocksPlacement_RegardlessOfCollisionMatrix(string layer, PlayerTurretPlacementResult expected)
    {
        GameObject actor = CreateActor(layer, _origin);
        int actorLayer = actor.GetComponentInChildren<BoxCollider2D>().gameObject.layer;
        bool wasIgnored = Physics2D.GetIgnoreLayerCollision(_player.layer, actorLayer);
        try
        {
            Physics2D.IgnoreLayerCollision(_player.layer, actorLayer, true);
            Physics2D.SyncTransforms();
            Assert.That(Evaluate(_origin), Is.EqualTo(expected));
            actor.GetComponentInChildren<BoxCollider2D>().isTrigger = true;
            Assert.That(CanPlace(_origin), Is.True, "Attack/detection triggers must not reserve placement space.");
        }
        finally { Physics2D.IgnoreLayerCollision(_player.layer, actorLayer, wasIgnored); }
    }

    [TestCase("Bullet")]
    [TestCase("EmenyBullet")]
    [TestCase("PlayerBulletCollider")]
    public void BulletLayersDoNotBlockPlacement(string layer)
    {
        GameObject bullet = CreateObject("Nonblocking projectile");
        bullet.layer = LayerMask.NameToLayer(layer);
        bullet.transform.position = _origin;
        bullet.AddComponent<BoxCollider2D>();
        Physics2D.SyncTransforms();
        Assert.That(CanPlace(_origin), Is.True);
    }

    [TestCase("Player", PlayerTurretPlacementResult.OverlapsPlayer)]
    [TestCase("Enemy", PlayerTurretPlacementResult.OverlapsEnemy)]
    public void OccupiedPlacementPreservesInventoryAndCooldown_AndCanSucceedAfterActorMoves(string layer, PlayerTurretPlacementResult expected)
    {
        PlayerTurretSkill skill = CreateSkill();
        GameObject actor = CreateActor(layer, skill.GetPlacementPosition(_origin));
        Assert.That(skill.TryUseSkill(), Is.True);
        Assert.That(skill.TryConfirmPlacement(_origin), Is.False);
        Assert.That(skill.IsPlacing, Is.True);
        Assert.That(skill.PlacementFeedback, Is.EqualTo(expected));
        Assert.That(PlayerSkillDescriptions.TurretPlacementFailure(expected), Does.Contain(layer == "Player" ? "플레이어" : "몬스터"));
        Assert.That(skill.AvailableCount, Is.EqualTo(3));
        Assert.That(skill.CurrentCooldownTimer, Is.Zero);
        actor.transform.position += Vector3.right * 8f;
        Assert.That(skill.TryConfirmPlacement(_origin), Is.True, "Confirmation must see this frame's moved actor.");
        Assert.That(skill.AvailableCount, Is.EqualTo(2));
        Assert.That(skill.CurrentCooldownTimer, Is.EqualTo(skill.SkillCooldown));
    }

    [Test]
    public void SkillWithInstallerBody_RejectsBodyAndInstallsAtChosenMousePosition()
    {
        var body = _player.AddComponent<BoxCollider2D>();
        body.size = new Vector2(8f, 8f);
        PlayerTurretSkill skill = CreateSkill();
        Assert.That(skill.TryUseSkill(), Is.True);
        Assert.That(skill.AvailableCount, Is.EqualTo(3));
        Assert.That(skill.TryConfirmPlacement(_origin), Is.False);
        body.size = Vector2.one;
        Vector3 chosenPosition = skill.GetPlacementPosition(_origin + Vector3.down * 100f);
        Assert.That(skill.TryConfirmPlacement(chosenPosition), Is.True);
        Assert.That(skill.AvailableCount, Is.EqualTo(2));
        int deployedCount = 0;
        foreach (TemporaryTurret turret in Object.FindObjectsByType<TemporaryTurret>(FindObjectsSortMode.None))
        {
            if (turret.gameObject.scene != _scene) continue;
            deployedCount++;
            Assert.That(turret.transform.position, Is.EqualTo(chosenPosition));
            Assert.That(PlayerTurretPlacementRules.TryGetFootprint(turret.gameObject, turret.transform.position, out Bounds footprint), Is.True);
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(footprint.center, footprint.size, 0f))
                Assert.That(hit.transform.IsChildOf(_player.transform), Is.False);
        }
        Assert.That(deployedCount, Is.EqualTo(1));
    }

    [Test]
    public void FailedPlacement_PreservesInventoryAndCooldown_ThenValidPlacementConsumesOnce()
    {
        PlayerTurretSkill skill = CreateSkill();
        _player.transform.position = _origin + Vector3.right * 11f;

        Assert.That(skill.TryUseSkill(), Is.True);
        Assert.That(skill.TryConfirmPlacement(_player.transform.position), Is.False);
        Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerTurretPlacementResult.OutsideGround));
        Assert.That(skill.AvailableCount, Is.EqualTo(3));
        Assert.That(skill.CurrentCooldownTimer, Is.Zero);

        _player.transform.position = _origin;
        Assert.That(skill.TryConfirmPlacement(_origin), Is.True);
        Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerTurretPlacementResult.Allowed),
            "Successful placement must immediately clear the old warning.");
        Assert.That(skill.AvailableCount, Is.EqualTo(2));
        skill.Tick(skill.SkillCooldown);
        Assert.That(skill.TryUseSkill(), Is.True);
        Assert.That(skill.TryConfirmPlacement(_origin), Is.False, "The same base area is already occupied.");
        Assert.That(skill.AvailableCount, Is.EqualTo(2));
        Assert.That(skill.CurrentCooldownTimer, Is.Zero);
        Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerTurretPlacementResult.OverlapsTurret));
        skill.CancelPlacement();
        Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerTurretPlacementResult.Allowed),
            "Cancelling placement must clear the warning immediately.");
    }

    [Test]
    public void PreviewAndCancellation_DoNotConsumeOrSpawnGameplayTurret()
    {
        PlayerTurretSkill skill = CreateSkill();
        Assert.That(skill.TryConfirmPlacement(_origin), Is.False, "Placement must start with the skill key.");
        Assert.That(skill.TryUseSkill(), Is.True);
        Assert.That(skill.IsPlacing, Is.True);
        Assert.That(skill.AvailableCount, Is.EqualTo(3));
        foreach (var turret in Object.FindObjectsByType<TemporaryTurret>(FindObjectsSortMode.None))
            Assert.That(turret.gameObject.scene, Is.Not.EqualTo(_scene));
        Assert.That(CanPlace(_origin), Is.True, "The ghost must not block its own placement.");
        skill.CancelPlacement();
        Assert.That(skill.IsPlacing, Is.False);
        Assert.That(skill.AvailableCount, Is.EqualTo(3));
        Assert.That(skill.CurrentCooldownTimer, Is.Zero);
        Assert.That(skill.TryUseSkill(), Is.True);
        skill.enabled = false;
        Assert.That(skill.IsPlacing, Is.False);
    }

    [Test]
    public void FarPointer_InstallsOnlyInAdjacentCell_AndConsumesOnce()
    {
        PlayerTurretSkill skill = CreateSkill();
        Assert.That(skill.TryUseSkill(), Is.True);
        Vector3 pointer = _origin + Vector3.right * 1000f;
        Vector3 expected = _origin + Vector3.right * 4f;
        Assert.That(skill.GetPlacementPosition(pointer), Is.EqualTo(expected));
        Assert.That(skill.TryConfirmPlacement(pointer), Is.True);
        Assert.That(skill.IsPlacing, Is.False);
        foreach (var turret in Object.FindObjectsByType<TemporaryTurret>(FindObjectsSortMode.None))
            if (turret.gameObject.scene == _scene) Assert.That(turret.transform.position, Is.EqualTo(expected));
        Assert.That(skill.TryConfirmPlacement(pointer), Is.False);
        Assert.That(skill.AvailableCount, Is.EqualTo(2));
    }

    [TestCase(1f, 0f)]
    [TestCase(1f, 1f)]
    [TestCase(0f, 1f)]
    [TestCase(-1f, 1f)]
    [TestCase(-1f, 0f)]
    [TestCase(-1f, -1f)]
    [TestCase(0f, -1f)]
    [TestCase(1f, -1f)]
    public void AdjacentGrid_SelectsEightCellsAndFollowsPlayer(float x, float y)
    {
        PlayerTurretSkill skill = CreateSkill();
        Vector3 direction = new(x, y, 0f);
        Vector3 near = skill.GetPlacementPosition(_origin + direction);
        Assert.That(near, Is.EqualTo(_origin + direction * 4f));
        Assert.That(skill.GetPlacementPosition(_origin + direction * 100f), Is.EqualTo(near));
        _player.transform.position += new Vector3(2f, -3f, 65f);
        Assert.That(skill.GetPlacementPosition(_player.transform.position + direction * 100f),
            Is.EqualTo(_player.transform.position + direction * 4f));
    }

    [TestCase(20f, 1f, 0f)]
    [TestCase(25f, 1f, 1f)]
    [TestCase(65f, 1f, 1f)]
    [TestCase(70f, 0f, 1f)]
    public void AdjacentGrid_UsesEqualAngularSectors(float degrees, float x, float y)
    {
        PlayerTurretSkill skill = CreateSkill();
        float angle = degrees * Mathf.Deg2Rad;
        Vector3 pointer = _origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 100f;
        Assert.That(skill.GetPlacementPosition(pointer), Is.EqualTo(_origin + new Vector3(x, y, 0f) * 4f));
    }

    [UnityTest]
    public IEnumerator InstalledTurret_BlocksPlayerAndEnemy_EvenWhenGlobalMatrixIgnoresThem()
    {
        GameObject turret = Object.Instantiate(_prefab, _origin, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(turret, _scene);
        turret.GetComponent<TemporaryTurret>().enabled = false;
        int deviceLayer = LayerMask.NameToLayer("PlayerFieldDevice");
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        bool ignoredPlayer = Physics2D.GetIgnoreLayerCollision(deviceLayer, playerLayer);
        bool ignoredEnemy = Physics2D.GetIgnoreLayerCollision(deviceLayer, enemyLayer);
        try
        {
            Physics2D.IgnoreLayerCollision(deviceLayer, playerLayer, true);
            Physics2D.IgnoreLayerCollision(deviceLayer, enemyLayer, true);
            var bodies = new Rigidbody2D[2];
            for (int i = 0; i < bodies.Length; i++)
            {
                GameObject actor = CreateObject(i == 0 ? "Moving Player" : "Moving Enemy");
                actor.layer = i == 0 ? playerLayer : enemyLayer;
                actor.transform.position = _origin + Vector3.right * (i == 0 ? -4f : 4f);
                actor.AddComponent<BoxCollider2D>();
                bodies[i] = actor.AddComponent<Rigidbody2D>();
                bodies[i].gravityScale = 0f;
                bodies[i].constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            Physics2D.SyncTransforms();
            for (int step = 0; step < 25; step++)
            {
                bodies[0].linearVelocity = Vector2.right * 10f;
                bodies[1].linearVelocity = Vector2.left * 10f;
                yield return new WaitForFixedUpdate();
            }
            Assert.That(bodies[0].position.x, Is.LessThan(_origin.x - 2f));
            Assert.That(bodies[1].position.x, Is.GreaterThan(_origin.x + 2f));
            var collider = turret.GetComponent<BoxCollider2D>();
            turret.GetComponent<TemporaryTurretHealth>().TakeDamage(100f);
            Assert.That(collider.enabled, Is.False, "Destroyed turrets must immediately release the occupied space.");
        }
        finally
        {
            Physics2D.IgnoreLayerCollision(deviceLayer, playerLayer, ignoredPlayer);
            Physics2D.IgnoreLayerCollision(deviceLayer, enemyLayer, ignoredEnemy);
        }
    }

    private PlayerTurretPlacementResult Evaluate(Vector3 position) =>
        PlayerTurretPlacementRules.Evaluate(_prefab, position, _player, _ground);

    private bool CanPlace(Vector3 position) =>
        PlayerTurretPlacementRules.CanPlace(_prefab, position, _player, _ground);

    private PlayerTurretSkill CreateSkill()
    {
        var skill = _player.AddComponent<PlayerTurretSkill>();
        var data = new SerializedObject(skill);
        data.FindProperty("turretPrefab").objectReferenceValue = _prefab;
        data.FindProperty("_placementGround").objectReferenceValue = _ground;
        data.FindProperty("_previewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/TemporaryTurretPreview.prefab").GetComponent<PlayerTurretPlacementPreview>();
        data.ApplyModifiedPropertiesWithoutUndo();
        return skill;
    }

    private GameObject CreateActor(string layer, Vector3 position)
    {
        GameObject actor = CreateObject("Occupying " + layer);
        actor.transform.position = position;
        actor.layer = LayerMask.NameToLayer(layer);
        if (layer == "Player") actor.AddComponent<PlayerInfo>().enabled = false;
        else
        {
            actor.AddComponent<Unity.Netcode.NetworkObject>();
            actor.AddComponent<EnemyHealth>().enabled = false;
        }
        GameObject body = CreateObject("Untagged child body");
        body.transform.SetParent(actor.transform, false);
        body.AddComponent<BoxCollider2D>();
        return actor;
    }

    private GameObject CreateObject(string name)
    {
        var obj = new GameObject(name);
        SceneManager.MoveGameObjectToScene(obj, _scene);
        return obj;
    }
}

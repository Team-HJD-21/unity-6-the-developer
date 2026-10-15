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
    public void WallBlocksPlacement_ButPlayerAndTriggerDoNot()
    {
        _player.AddComponent<BoxCollider2D>();
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
    public void FailedPlacement_PreservesInventoryAndCooldown_ThenValidPlacementConsumesOnce()
    {
        PlayerTurretSkill skill = _player.AddComponent<PlayerTurretSkill>();
        var data = new SerializedObject(skill);
        data.FindProperty("turretPrefab").objectReferenceValue = _prefab;
        data.FindProperty("_placementGround").objectReferenceValue = _ground;
        data.ApplyModifiedPropertiesWithoutUndo();
        _player.transform.position = _origin + Vector3.right * 11f;

        Assert.That(skill.TryUseSkill(), Is.False);
        Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerTurretPlacementResult.OutsideGround));
        Assert.That(skill.AvailableCount, Is.EqualTo(3));
        Assert.That(skill.CurrentCooldownTimer, Is.Zero);

        _player.transform.position = _origin;
        Assert.That(skill.TryUseSkill(), Is.True);
        Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerTurretPlacementResult.Allowed),
            "Successful placement must immediately clear the old warning.");
        Assert.That(skill.AvailableCount, Is.EqualTo(2));
        skill.Tick(skill.SkillCooldown);
        Assert.That(skill.TryUseSkill(), Is.False, "The same base area is already occupied.");
        Assert.That(skill.AvailableCount, Is.EqualTo(2));
        Assert.That(skill.CurrentCooldownTimer, Is.Zero);
        Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerTurretPlacementResult.OverlapsTurret));
        skill.Tick(2.51f);
        Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerTurretPlacementResult.Allowed),
            "Failure feedback must expire even when no new placement is attempted.");
    }

    private PlayerTurretPlacementResult Evaluate(Vector3 position) =>
        PlayerTurretPlacementRules.Evaluate(_prefab, position, _player, _ground);

    private bool CanPlace(Vector3 position) =>
        PlayerTurretPlacementRules.CanPlace(_prefab, position, _player, _ground);

    private GameObject CreateObject(string name)
    {
        var obj = new GameObject(name);
        SceneManager.MoveGameObjectToScene(obj, _scene);
        return obj;
    }
}

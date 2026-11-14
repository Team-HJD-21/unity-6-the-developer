using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed class PlayerHoverFieldFeedbackTests
{
    private Scene _scene;
    private GameObject _player, _grid;
    private PlayerHoverFieldFeedback _feedback;
    private Tilemap _ground, _plants;
    private Tile _landTile, _plantTile;
    private SpriteRenderer[] _dust, _leaves;
    private readonly Vector3 _origin = new Vector3(50000, 50000);
    private readonly Vector3Int _plantCell = new Vector3Int(2, 0, 0);
    private Matrix4x4 _tileTransform;
    private Color _tileColor;
    private TileFlags _tileFlags;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _scene = SceneManager.CreateScene("Hover feedback fixture");
        _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        SceneManager.MoveGameObjectToScene(_player, _scene);
        _player.GetComponent<PlayerInputHandler>().enabled = false;
        _player.GetComponent<PlayerMovement>().enabled = false;
        _player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        _feedback = _player.GetComponent<PlayerHoverFieldFeedback>();
        var data = new SerializedObject(_feedback);
        _landTile = ScriptableObject.CreateInstance<Tile>();
        _landTile.sprite = (Sprite)data.FindProperty("_grassSprites").GetArrayElementAtIndex(0).objectReferenceValue;
        _landTile.colliderType = Tile.ColliderType.Grid;
        _plantTile = ScriptableObject.CreateInstance<Tile>();
        _plantTile.sprite = (Sprite)data.FindProperty("_vegetationSprites").GetArrayElementAtIndex(0).objectReferenceValue;
        _plantTile.colliderType = Tile.ColliderType.Sprite;
        _grid = new GameObject("Grid", typeof(Grid)); SceneManager.MoveGameObjectToScene(_grid, _scene); _grid.transform.position = _origin;
        _ground = Map("Ground"); _plants = Map("Plants");
        for (int x = -8; x < 8; x++) for (int y = -4; y < 4; y++) _ground.SetTile(new Vector3Int(x, y), _landTile);
        _plants.SetTile(_plantCell, _plantTile);
        _tileColor = _plants.GetColor(_plantCell); _tileFlags = _plants.GetTileFlags(_plantCell); _tileTransform = _plants.GetTransformMatrix(_plantCell);
        SpriteRenderer[] Displays(string name) { var p = data.FindProperty(name); return Enumerable.Range(0, p.arraySize).Select(i => (SpriteRenderer)p.GetArrayElementAtIndex(i).objectReferenceValue).ToArray(); }
        _dust = Displays("_dustDisplays"); _leaves = Displays("_vegetationDisplays");
        Stand(new Vector3(.5f, .5f)); _feedback.Tick(.01f);
    }

    private Tilemap Map(string name)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        go.transform.SetParent(_grid.transform, false); return go.GetComponent<Tilemap>();
    }
    private void Stand(Vector3 point)
    {
        Physics2D.SyncTransforms();
        _player.transform.position += _origin + point - (Vector3)_player.GetComponent<PlayerDirectionalColliders>().GroundPosition;
        Physics2D.SyncTransforms();
    }
    private void MoveAndDisturb() { Stand(new Vector3(1.3f, .5f)); _feedback.Tick(.1f); _feedback.Tick(.08f); }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_player != null) Object.DestroyImmediate(_player);
        if (_grid != null) Object.DestroyImmediate(_grid);
        Object.DestroyImmediate(_landTile); Object.DestroyImmediate(_plantTile);
        if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
        yield return new ExitPlayMode();
    }

    [Test]
    public void OnlyActualMovementEmitsOnSupportedLandAndStoppingLetsItFade()
    {
        for (int i = 0; i < 10; i++) _feedback.Tick(.1f);
        Assert.That(_dust.Any(d => d.enabled), Is.False);
        MoveAndDisturb();
        Assert.That(_dust.Any(d => d.enabled && d.color.a > 0), Is.True);
        for (int i = 0; i < 10; i++) _feedback.Tick(.1f);
        Assert.That(_dust.Any(d => d.enabled), Is.False);
        Assert.That(_leaves.Any(d => d.enabled), Is.False);
        Stand(new Vector3(30, 30)); _feedback.Tick(.1f);
        Stand(new Vector3(31, 30)); _feedback.Tick(.1f);
        Assert.That(_dust.Any(d => d.enabled), Is.False, "No trail over absent ground/water.");
    }

    [Test]
    public void MovementLeavesAnEvenDirectionalWakeThatThinsInsteadOfPuffingUp()
    {
        // One long frame must still leave several spaced samples along the path.
        Stand(new Vector3(2.5f, .5f)); _feedback.Tick(.25f);
        var trail = _dust.Where(d => d.enabled).ToArray();
        Assert.That(trail.Length, Is.GreaterThanOrEqualTo(5));
        Assert.That(trail.Max(d => d.transform.position.x) - trail.Min(d => d.transform.position.x), Is.GreaterThan(1f));
        Assert.That(trail.All(d => Vector2.Dot(d.transform.right, Vector2.right) > .99f), Is.True);

        var newest = trail.OrderByDescending(d => d.transform.position.x).First();
        float thickness = newest.transform.localScale.y;
        _feedback.Tick(.04f);
        Assert.That(newest.enabled, Is.True);
        Assert.That(newest.transform.localScale.y, Is.LessThan(thickness), "A movement wake thins instead of rising like an impact cloud.");

        // Turning changes new samples, while the old trail keeps its direction.
        Stand(new Vector3(2.5f, 1.2f)); _feedback.Tick(.1f);
        Assert.That(_dust.Any(d => d.enabled && Vector2.Dot(d.transform.right, Vector2.up) > .99f), Is.True);
        Assert.That(_dust.Any(d => d.enabled && Vector2.Dot(d.transform.right, Vector2.right) > .99f), Is.True);
        _feedback.Tick(.4f);
        Assert.That(_dust.Any(d => d.enabled), Is.False);
    }

    [Test]
    public void VegetationSwaysWithoutChangingTileTransformOrCollisionAndDisableRestoresColor()
    {
        _plants.GetComponent<TilemapCollider2D>().ProcessTilemapChanges(); Physics2D.SyncTransforms();
        Bounds bounds = _plants.GetComponent<TilemapCollider2D>().bounds;
        MoveAndDisturb();
        Assert.That(_leaves.Any(d => d.enabled), Is.True);
        Assert.That(_plants.GetColor(_plantCell).a, Is.Zero);
        Assert.That(_plants.GetTransformMatrix(_plantCell), Is.EqualTo(_tileTransform));
        _plants.GetComponent<TilemapCollider2D>().ProcessTilemapChanges(); Physics2D.SyncTransforms();
        Assert.That(_plants.GetComponent<TilemapCollider2D>().bounds, Is.EqualTo(bounds));
        _feedback.enabled = false;
        Assert.That(_plants.GetColor(_plantCell), Is.EqualTo(_tileColor));
        Assert.That(_plants.GetTileFlags(_plantCell), Is.EqualTo(_tileFlags));
        Assert.That(_leaves.Any(d => d.enabled), Is.False);
    }

    [Test]
    public void SpaceshipAndDeathClearExistingEffectsAndDoNotEmitNewOnes()
    {
        MoveAndDisturb();
        _player.GetComponent<PlayerLocationState>().CheckLocation(true); _feedback.Tick(.1f);
        Assert.That(_dust.Any(d => d.enabled) || _leaves.Any(d => d.enabled), Is.False);
        Assert.That(_plants.GetColor(_plantCell), Is.EqualTo(_tileColor));
        Stand(new Vector3(2.3f, .5f)); _feedback.Tick(.1f);
        Assert.That(_dust.Any(d => d.enabled), Is.False);
        _player.GetComponent<PlayerLocationState>().CheckLocation(false); _feedback.Tick(.1f);
        Stand(new Vector3(1.3f, .5f)); _feedback.Tick(.1f);
        Assert.That(_dust.Any(d => d.enabled), Is.True);
        _player.GetComponent<PlayerInfo>().curHp = 0; _feedback.Tick(.1f);
        Assert.That(_dust.Any(d => d.enabled) || _leaves.Any(d => d.enabled), Is.False);
    }

    [Test]
    public void FieldPauseFreezesEffectsAndTeleportClearsTheTrail()
    {
        var clock = new PlayerAutomaticRecoveryTests.Clock(); FieldSimulationActor.Attach(_player, clock, destroyOnCompletion: false);
        MoveAndDisturb();
        var display = _dust.First(d => d.enabled); Vector3 position = display.transform.position; Color color = display.color;
        clock.Paused = true; _feedback.Tick(2);
        Assert.That(display.transform.position, Is.EqualTo(position)); Assert.That(display.color, Is.EqualTo(color));
        clock.Paused = false; Stand(new Vector3(60, 60)); _feedback.Tick(.1f);
        Assert.That(_dust.Any(d => d.enabled) || _leaves.Any(d => d.enabled), Is.False);
        Assert.That(_plants.GetColor(_plantCell), Is.EqualTo(_tileColor));
    }
}

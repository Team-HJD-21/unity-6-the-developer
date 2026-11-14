using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PlayerGroundMovementTests
{
    private GameObject _grid;
    private Tilemap _ground;
    private Tile _tile;

    [SetUp]
    public void SetUp()
    {
        _grid = new GameObject("Ground movement grid", typeof(Grid));
        var map = new GameObject("Land", typeof(Tilemap));
        map.transform.SetParent(_grid.transform, false);
        _ground = map.GetComponent<Tilemap>();
        _tile = ScriptableObject.CreateInstance<Tile>();
        for (int y = -3; y <= 3; y++)
        for (int x = -3; x <= 3; x++) _ground.SetTile(new Vector3Int(x, y), _tile);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_grid);
        Object.DestroyImmediate(_tile);
    }

    [Test]
    public void LandMovementIsUnchangedAndFootprintStopsBeforeWater()
    {
        var feet = new Bounds(new Vector3(3.5f, .5f, -15), new Vector3(.8f, .4f));
        Assert.That(PlayerGroundMovement.ConstrainVelocity(_ground, feet, Vector2.right, .05f), Is.EqualTo(Vector2.right));
        Vector2 velocity = PlayerGroundMovement.ConstrainVelocity(_ground, feet, Vector2.right * 5, .1f);
        Assert.That(velocity.x, Is.InRange(.9f, 1.01f));
        feet.center += (Vector3)(velocity * .1f);
        Assert.That(feet.max.x, Is.LessThan(4));
        Assert.That(PlayerTurretPlacementRules.IsOnGround(_ground, feet), Is.True);
    }

    [Test]
    public void DashCannotSkipWaterEvenWhenItsDestinationIsLand()
    {
        for (int y = -3; y <= 3; y++) _ground.SetTile(new Vector3Int(1, y), null);
        var feet = new Bounds(new Vector3(.5f, .5f), new Vector3(.4f, .4f));
        var velocity = PlayerGroundMovement.ConstrainVelocity(_ground, feet, Vector2.right * 30, .1f);
        Assert.That(velocity.x, Is.InRange(2.9f, 3.01f));
        Assert.That(feet.max.x + velocity.x * .1f, Is.LessThan(1));
    }

    [Test]
    public void DiagonalMovementCanSlideAlongTheShore()
    {
        var feet = new Bounds(new Vector3(3.79f, .5f), new Vector3(.4f, .4f));
        var velocity = PlayerGroundMovement.ConstrainVelocity(_ground, feet, new Vector2(5, 5), .1f);
        Assert.That(velocity.x, Is.Zero);
        Assert.That(velocity.y, Is.EqualTo(5));
    }

    [Test]
    public void DiagonalCannotCutAcrossAMissingCorner()
    {
        _ground.SetTile(new Vector3Int(1, 0), null);
        _ground.SetTile(new Vector3Int(0, 1), null);
        var feet = new Bounds(new Vector3(.5f, .5f), new Vector3(.2f, .2f));
        var velocity = PlayerGroundMovement.ConstrainVelocity(_ground, feet, new Vector2(10, 10), .1f);
        Assert.That(velocity.x, Is.LessThan(4.01f));
        Assert.That(velocity.y, Is.LessThan(4.01f));
    }

    [Test]
    public void ScaledGridAndNegativeCellsUseWorldFootPosition()
    {
        _grid.transform.position = new Vector3(-20, 10);
        _grid.transform.localScale = new Vector3(2, 2, 1);
        var feet = new Bounds(new Vector3(-25.5f, 10.5f), new Vector3(.4f, .4f));
        var velocity = PlayerGroundMovement.ConstrainVelocity(_ground, feet, Vector2.left * 30, .1f);
        Assert.That(velocity.x, Is.InRange(-3.01f, -2.9f));
    }

    [Test]
    public void SceneWithoutGroundKeepsExistingMovement()
    {
        Assert.That(PlayerGroundMovement.ConstrainVelocity(null, new Bounds(), new Vector2(4, 2), .02f),
            Is.EqualTo(new Vector2(4, 2)));
    }
}

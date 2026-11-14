using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

public sealed class PlayerGroundRuntimeTests
{
    private Scene _scene;
    private Keyboard _keyboard;
    private Tile _tile;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
        if (_tile != null) Object.DestroyImmediate(_tile);
        if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ActualPlayerWalkingAndDashStayOnLandAndHubMovementIsUnaffected()
    {
        yield return new EnterPlayMode();
        _scene = SceneManager.CreateScene("Player shore test");
        _keyboard = InputSystem.AddDevice<Keyboard>();
        var grid = new GameObject("Shore grid", typeof(Grid));
        SceneManager.MoveGameObjectToScene(grid, _scene);
        grid.transform.position = new Vector3(17000, 17000);
        var map = new GameObject("Land", typeof(Tilemap), typeof(TilemapCollider2D));
        map.transform.SetParent(grid.transform, false);
        var ground = map.GetComponent<Tilemap>();
        _tile = ScriptableObject.CreateInstance<Tile>();
        _tile.colliderType = Tile.ColliderType.None;
        for (int y = -4; y <= 4; y++)
        for (int x = -4; x <= 1; x++) ground.SetTile(new Vector3Int(x, y), _tile);
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        SceneManager.MoveGameObjectToScene(player, _scene);
        var body = player.GetComponent<Rigidbody2D>();
        body.position = grid.transform.position;
        var feet = player.GetComponent<PlayerDirectionalColliders>();
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.D));
        InputSystem.Update();
        float until = Time.realtimeSinceStartup + .7f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Assert.That(body.position.x, Is.GreaterThan(17000.1f), "Walking must still advance on land. input=" +
            player.GetComponent<PlayerInputHandler>().MoveInput + ", canMove=" + player.GetComponent<PlayerMovement>().CanAcceptInput +
            ", velocity=" + body.linearVelocity + ", feet=" + feet.GroundBounds);
        Assert.That(feet.GroundBounds.max.x, Is.LessThanOrEqualTo(17002), "Walking must stop before water.");
        body.position = grid.transform.position;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        Assert.That(player.GetComponent<PlayerDash>().TryDash(), Is.True);
        until = Time.realtimeSinceStartup + .4f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Assert.That(feet.GroundBounds.max.x, Is.LessThanOrEqualTo(17002), "Dash must obey the same shoreline.");
        // Core hub binding intentionally leaves walking enabled while disabling field actions.
        using var inputLocks = new TeamHJD.Game.Application.InputLockService();
        player.GetComponent<PlayerControlState>().BindHub(inputLocks);
        until = Time.realtimeSinceStartup + .6f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Assert.That(body.position.x, Is.GreaterThan(17002), "The field constraint must not trap the hub actor.");
    }
}

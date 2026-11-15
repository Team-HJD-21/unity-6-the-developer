using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerHitProtectionRuntimeTests
{
    private Scene _scene;
    private Clock _clock;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _scene = SceneManager.CreateScene("Hit protection physics", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        _clock = new Clock();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var root in _scene.GetRootGameObjects()) Object.Destroy(root);
        yield return null; yield return SceneManager.UnloadSceneAsync(_scene); yield return new ExitPlayMode();
    }

    private GameObject Create(string name, Vector2 position, string layer)
    {
        var result = new GameObject(name); SceneManager.MoveGameObjectToScene(result, _scene);
        result.layer = LayerMask.NameToLayer(layer); result.transform.position = position; return result;
    }

    [UnityTest]
    public IEnumerator PhysicsPassesThroughEnemyBodiesButKeepsWorldWallsAndRestoresEnemyContacts()
    {
        var actor = Create("Protected Player", Vector2.left * 2, "PlayerFeetBox");
        var body = actor.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
        var feet = actor.AddComponent<BoxCollider2D>(); feet.size = Vector2.one * .4f;
        feet.includeLayers = LayerMask.GetMask("EnemyFeetBox", "WorldGeometry");
        var health = actor.AddComponent<PlayerInfo>(); FieldSimulationActor.Attach(actor, _clock, destroyOnCompletion: false);
        Assert.That(health.ApplyDamage(1), Is.True);

        // 보호 시작 뒤 생성된 적도 별도 pair 등록 없이 제외되어야 합니다.
        var enemy = Create("New enemy body", Vector2.zero, "EnemyFeetBox").AddComponent<BoxCollider2D>();
        enemy.includeLayers = LayerMask.GetMask("PlayerFeetBox");
        var wall = Create("World wall", Vector2.right * 2, "WorldGeometry").AddComponent<BoxCollider2D>();
        wall.size = new Vector2(.3f, 4); wall.includeLayers = LayerMask.GetMask("PlayerFeetBox");
        Physics2D.SyncTransforms();
        Move();
        Assert.That(body.position.x, Is.GreaterThan(1), "Protection must let the Player move through the enemy body.");
        Assert.That(body.position.x, Is.LessThan(1.9f), "World geometry must still stop the protected Player.");
        _clock.Now = 3;
        typeof(PlayerInfo).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, null);
        body.position = Vector2.left * 2; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
        Move();
        Assert.That(body.position.x, Is.LessThan(-.6f), "After expiration, the enemy body must block the Player again.");
        void Move()
        {
            for (int i = 0; i < 70; i++)
            {
                body.linearVelocity = Vector2.right * 3;
                Assert.That(_scene.GetPhysicsScene2D().Simulate(.02f), Is.True);
            }
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator AuthoredPlayerBothFacingsAndWeaponBlinkWithoutAffectingAnotherPlayer()
    {
        var first = Spawn(Vector3.zero); var second = Spawn(Vector3.right * 10);
        var health = first.GetComponent<PlayerInfo>(); var sprite = first.GetComponent<SpriteRenderer>();
        var original = sprite.color; var own = Masks(first); var other = Masks(second);
        Assert.That(health.ApplyDamage(1), Is.True); yield return null;
        foreach (var facing in new[] { PlayerAimFacing.Left, PlayerAimFacing.Right })
        {
            first.GetComponent<PlayerDirectionalColliders>().SetFacing(facing);
            foreach (var collider in own.Keys)
                Assert.That(collider.excludeLayers.value & LayerMask.GetMask("Enemy", "EnemyFeetBox"), Is.EqualTo(LayerMask.GetMask("Enemy", "EnemyFeetBox")));
        }
        Assert.That(sprite.color.r, Is.EqualTo(original.r * .55f).Within(.001));
        Assert.That(first.GetComponent<PlayerWeaponVisual>().Renderer.color, Is.EqualTo(sprite.color));
        foreach (var entry in other) Assert.That(entry.Key.excludeLayers.value, Is.EqualTo(entry.Value));
        _clock.Now = 3; yield return new WaitForFixedUpdate(); yield return null;
        foreach (var entry in own) Assert.That(entry.Key.excludeLayers.value, Is.EqualTo(entry.Value));
        Assert.That(sprite.color, Is.EqualTo(original));

        GameObject Spawn(Vector3 at)
        {
            var result = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), at, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(result, _scene);
            result.GetComponent<PlayerInputHandler>().enabled = false; result.GetComponent<PlayerMovement>().enabled = false;
            result.GetComponent<Rigidbody2D>().simulated = false; FieldSimulationActor.Attach(result, _clock, destroyOnCompletion: false); return result;
        }
        Dictionary<Collider2D, int> Masks(GameObject actor)
        {
            var result = new Dictionary<Collider2D, int>();
            foreach (var collider in actor.GetComponentsInChildren<Collider2D>(true)) if (!collider.isTrigger) result.Add(collider, collider.excludeLayers.value);
            return result;
        }
    }

    private sealed class Clock : IMatchTime
    {
        public double Now; private readonly Guid _epoch = Guid.NewGuid();
        public bool IsAvailable => true; public double ElapsedSeconds => Now;
        public MatchTimeSnapshot Snapshot => new(new MatchId("hit-runtime"), _epoch, 0, Now, Now, 1, false, true);
    }
}

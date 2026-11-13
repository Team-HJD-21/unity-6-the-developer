using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Enemy.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class PlayerEnemyDetectorTests
{
    private Scene _scene;
    private PhysicsScene2D _physics;
    private readonly List<MatchPresenceService> _services = new();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private sealed class Clock : IMatchTime
    {
        public double ElapsedSeconds => 0;
        public bool IsAvailable => true;
        public MatchTimeSnapshot Snapshot { get; set; }
        public Clock(string id = "detector-test") => Snapshot = new MatchTimeSnapshot(new MatchId(id), Guid.NewGuid(), 0, 0, 0, 1, false, true);
        public void Pause() => Snapshot = new MatchTimeSnapshot(Snapshot.MatchId, Snapshot.Epoch, 1, 0, 0, 0, true, true);
    }

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _physics = _scene.GetPhysicsScene2D();
        Assert.That(_physics.Equals(Physics2D.defaultPhysicsScene), Is.False);
    }
    [TearDown]
    public void TearDown()
    {
        foreach (var root in _scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        EditorSceneManager.ClosePreviewScene(_scene);
        foreach (var service in _services) service.Dispose();
        _services.Clear();
    }

    [Test]
    public void PlayerPrefabHasAnEnemyOnlyTriggerOutsideItsDamageHitboxes()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        var detector = prefab.GetComponentInChildren<PlayerEnemyDetector>(true);
        Assert.That(detector, Is.Not.Null);
        var circle = detector.GetComponent<CircleCollider2D>();
        Assert.That(circle.isTrigger, Is.True);
        Assert.That(circle.attachedRigidbody, Is.EqualTo(prefab.GetComponent<Rigidbody2D>()));
        Assert.That(circle.radius * detector.transform.lossyScale.x, Is.EqualTo(3).Within(.001f));
        Assert.That(detector.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Player")));
        Assert.That(Physics2D.GetIgnoreLayerCollision(detector.gameObject.layer, 7), Is.False);
        Assert.That(circle.excludeLayers.value & (1 << 16), Is.Not.Zero, "Enemy projectiles must not hit the detection range.");
        var data = new SerializedObject(detector);
        Assert.That(data.FindProperty("_owner").objectReferenceValue, Is.EqualTo(prefab.transform));
        Assert.That(data.FindProperty("_range").objectReferenceValue, Is.EqualTo(circle));
    }

    [Test]
    public void MultipleEnemyCollidersRemainDetectedUntilTheLastBodyLeaves()
    {
        var detector = Detector(Vector2.zero);
        var target = Enemy(new Vector2(1, 0));
        var second = target.gameObject.AddComponent<CircleCollider2D>(); second.radius = .2f;
        Step(detector);
        Assert.That(target.IsDetected, Is.True);
        target.GetComponent<BoxCollider2D>().enabled = false;
        Step(detector); Assert.That(target.IsDetected, Is.True);
        second.enabled = false;
        Step(detector); Assert.That(target.IsDetected, Is.False);
    }

    [Test]
    public void LastPlayerLeavingControlsDetectionAndAssassinDamagePermission()
    {
        var a = Detector(Vector2.zero); var b = Detector(Vector2.zero);
        var target = Enemy(new Vector2(1, 0));
        var rule = target.gameObject.AddComponent<EnemyAssassinDamageRule>(); Invoke(rule, "Awake");
        var health = target.gameObject.AddComponent<EnemyHealth>(); health.Initialize();
        Assert.That(health.CanBeAttackedBy(AttackSourceType.Player), Is.False);
        Step(a, b);
        Assert.That(health.CanBeAttackedBy(AttackSourceType.Player), Is.True);
        Assert.That(health.CanBeAttackedBy(AttackSourceType.Turret), Is.False);
        a.transform.parent.position = Vector3.right * 20; Step(a, b);
        Assert.That(target.IsDetected, Is.True);
        b.transform.parent.position = Vector3.right * 20; Step(a, b);
        Assert.That(health.CanBeAttackedBy(AttackSourceType.Player), Is.False);
    }

    [TestCase("range")] [TestCase("player")] [TestCase("death")]
    public void PlayerRangeDisableAndDeathReleaseTheirRegistrations(string change)
    {
        var detector = Detector(Vector2.zero); var target = Enemy(Vector2.right);
        Step(detector); Assert.That(target.IsDetected, Is.True);
        if (change == "range") detector.GetComponent<CircleCollider2D>().enabled = false;
        else if (change == "player") { detector.enabled = false; Invoke(detector, "OnDisable"); }
        else detector.transform.parent.GetComponent<PlayerInfo>().Die();
        Step(detector); Assert.That(target.IsDetected, Is.False);
    }

    [Test]
    public void ReenabledEnemyInsideRangeRestoresTheRegistrationAfterPoolReset()
    {
        var detector = Detector(Vector2.zero); var target = Enemy(Vector2.right);
        Step(detector); Assert.That(target.IsDetected, Is.True);
        Invoke(target, "OnDisable"); Assert.That(target.IsDetected, Is.False);
        Step(detector); Assert.That(target.IsDetected, Is.True);
        target.enabled = false;
        Step(detector); Assert.That(target.IsDetected, Is.False);
    }

    [Test]
    public void EnemySensorTriggersDoNotRevealADistantAssassin()
    {
        var detector = Detector(Vector2.zero); var target = Enemy(Vector2.right * 20);
        var sensor = target.gameObject.AddComponent<CircleCollider2D>(); sensor.isTrigger = true; sensor.radius = 30;
        Step(detector); Assert.That(target.IsDetected, Is.False);
    }

    [Test]
    public void ClientReplicaCannotChangeTheReceivedDetectionState()
    {
        var detector = Detector(Vector2.zero); var target = Enemy(Vector2.right);
        detector.transform.parent.GetComponent<PlayerMatchPresence>().BindReplica(new Clock(), default);
        Step(detector); Assert.That(target.IsDetected, Is.False);
        target.RegisterDetector(123);
        Step(detector); Assert.That(target.IsDetected, Is.True);
        target.UnregisterDetector(123);
        Assert.That(target.IsDetected, Is.False);
    }

    [Test]
    public void PausePreservesDetectionButEnteringSpaceshipReleasesIt()
    {
        var detector = Detector(Vector2.zero); var target = Enemy(Vector2.right);
        var clock = new Clock(); BindHost(detector, clock);
        FieldSimulationActor.Attach(target.gameObject, clock);
        Step(detector); Assert.That(target.IsDetected, Is.True);
        clock.Pause(); target.transform.position = Vector3.right * 20;
        Step(detector); Assert.That(target.IsDetected, Is.True);
        detector.transform.parent.GetComponent<PlayerMatchPresence>().Report(MatchPlayerLocation.Spaceship);
        Step(detector); Assert.That(target.IsDetected, Is.False);
    }

    [Test]
    public void DetectionContinuesWhileAnEnemyProjectileOverlapsWithoutHittingTheSensor()
    {
        var detector = Detector(Vector2.zero); var target = Enemy(Vector2.right);
        var playerBody = detector.transform.parent.GetComponent<Rigidbody2D>();
        playerBody.bodyType = RigidbodyType2D.Dynamic; playerBody.gravityScale = 0;
        var projectile = Root("Enemy projectile", Vector2.up);
        projectile.layer = LayerMask.NameToLayer("EmenyBullet");
        projectile.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var hitbox = projectile.AddComponent<BoxCollider2D>(); hitbox.isTrigger = true;
        Step(detector);
        var range = detector.GetComponent<CircleCollider2D>();
        Assert.That(target.IsDetected, Is.True, "An overlapping projectile must not interrupt assassin detection.");
        Assert.That(range.IsTouching(hitbox), Is.False);
    }

    [Test]
    public void DifferentMatchEnemyIsNotRegistered()
    {
        var detector = Detector(Vector2.zero); var target = Enemy(Vector2.right);
        BindHost(detector, new Clock()); FieldSimulationActor.Attach(target.gameObject, new Clock("other-match"));
        Step(detector); Assert.That(target.IsDetected, Is.False);
    }

    private void BindHost(PlayerEnemyDetector detector, Clock clock)
    {
        var player = new PlayerId("detector-owner");
        var config = new MatchConfig(clock.Snapshot.MatchId, MatchMode.Cooperative, new StageId("stage"),
            new DefinitionId("difficulty"), "test", 1, new[] { new PlayerLoadoutSnapshot(player, null) });
        var service = new MatchPresenceService(config, () => true, _ => { }); _services.Add(service);
        detector.transform.parent.GetComponent<PlayerMatchPresence>().Bind(service.RegisterActor(player), clock, default);
    }
    private PlayerEnemyDetector Detector(Vector2 position)
    {
        var owner = Root("Player", position); owner.AddComponent<Rigidbody2D>().gravityScale = 0;
        owner.layer = LayerMask.NameToLayer("Player");
        var health = owner.AddComponent<PlayerInfo>(); Invoke(health, "Awake");
        owner.AddComponent<PlayerMatchPresence>();
        var go = new GameObject("Assassin Detection Range"); go.transform.SetParent(owner.transform, false);
        go.layer = LayerMask.NameToLayer("Player");
        var circle = go.AddComponent<CircleCollider2D>(); circle.isTrigger = true; circle.radius = 3;
        circle.excludeLayers = ~(1 << 7); circle.layerOverridePriority = 1;
        var detector = go.AddComponent<PlayerEnemyDetector>(); Invoke(detector, "Awake"); Invoke(detector, "OnEnable");
        return detector;
    }
    private EnemyDetection Enemy(Vector2 position)
    {
        var go = Root("Assassin", position); go.layer = LayerMask.NameToLayer("Enemy");
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .5f;
        return go.AddComponent<EnemyDetection>();
    }
    private GameObject Root(string name, Vector2 position)
    {
        var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, _scene); go.transform.position = position;
        return go;
    }
    private void Step(params PlayerEnemyDetector[] detectors)
    {
        Physics2D.SyncTransforms();
        _physics.Simulate(Time.fixedDeltaTime);
        foreach (var detector in detectors) detector.RefreshDetection();
    }
    private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, Private).Invoke(owner, null);
}

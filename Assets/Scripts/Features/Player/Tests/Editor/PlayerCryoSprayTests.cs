using System;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Enemy.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class PlayerCryoSprayTests
{
    private Scene _scene;
    private PlayerCryoSpray _spray;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _spray = Create("Spray owner", Vector2.zero).AddComponent<PlayerCryoSpray>();
        typeof(PlayerCryoSpray).GetField("_vfxPrefab", Private).SetValue(_spray,
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/CryoSprayVfx.prefab"));
        Call("Awake");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var root in _scene.GetRootGameObjects()) if (root != null) Object.DestroyImmediate(root);
        EditorSceneManager.ClosePreviewScene(_scene);
    }

    [TestCase(0f)] [TestCase(45f)] [TestCase(90f)] [TestCase(180f)] [TestCase(-90f)]
    public void TaperedAreaDamagesOnlyBodiesInsideItsRange(float angle)
    {
        Quaternion rotation = Quaternion.Euler(0, 0, angle);
        Vector2 direction = rotation * Vector2.right;
        var inside = Enemy(rotation * new Vector2(2, .35f));
        var outside = Enemy(rotation * new Vector2(2, 1.6f));
        var distant = Enemy(direction * 5.5f);
        var behind = Enemy(-direction);
        // One health owns two hitboxes; both are queried but damage is applied once.
        inside.gameObject.AddComponent<CircleCollider2D>().radius = .1f;
        Assert.That(_spray.ApplyDamage(Vector2.zero, direction, 3), Is.True);
        Assert.That(inside.CurrentHealth, Is.EqualTo(inside.MaxHealth - 3));
        foreach (var untouched in new[] { outside, distant, behind })
            Assert.That(untouched.CurrentHealth, Is.EqualTo(untouched.MaxHealth));
    }

    [Test]
    public void FrontBodyBlocksTheSameLaneWhileOtherSprayLanesCanHit()
    {
        var front = Enemy(new Vector2(1.3f, 0), .3f);
        var behind = Enemy(new Vector2(2.7f, 0));
        var beside = Enemy(new Vector2(2.7f, .75f));
        Assert.That(_spray.ApplyDamage(Vector2.zero, Vector2.right, 20), Is.True);
        Assert.That(front.CurrentHealth, Is.Zero);
        Assert.That(behind.CurrentHealth, Is.EqualTo(behind.MaxHealth), "A kill must not leak through in the same damage tick.");
        Assert.That(beside.CurrentHealth, Is.Zero, "Another visible lane of the spray still deals damage.");
        _spray.ApplyDamage(Vector2.zero, Vector2.right, 3);
        Assert.That(behind.CurrentHealth, Is.EqualTo(behind.MaxHealth - 3), "A dead body no longer shields the next tick.");
    }

    [Test]
    public void SensorAndMovementBodyCannotTakeOrBlockSprayDamage()
    {
        var sensor = Enemy(new Vector2(.7f, 0), .4f, "EnemySensor");
        var movement = Enemy(new Vector2(1.4f, 0), .4f, "Enemy");
        var target = Enemy(new Vector2(2.5f, 0));
        _spray.ApplyDamage(Vector2.zero, Vector2.right, 3);
        Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 3));
        Assert.That(sensor.CurrentHealth, Is.EqualTo(sensor.MaxHealth));
        Assert.That(movement.CurrentHealth, Is.EqualTo(movement.MaxHealth));
    }

    [TestCase(false)] [TestCase(true)]
    public void WallAndMuzzleInsideWallStopDamageEvenWithoutStartInsideRaycasts(bool muzzleInside)
    {
        var target = Enemy(new Vector2(2.5f, 0));
        var wall = Create("Forest body", new Vector2(muzzleInside ? 0 : 1, 0));
        wall.layer = LayerMask.NameToLayer("Forest");
        wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 3);
        bool original = Physics2D.queriesStartInColliders;
        try
        {
            Physics2D.queriesStartInColliders = false;
            Assert.That(_spray.ApplyDamage(Vector2.zero, Vector2.right, 3), Is.True);
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth));
        }
        finally { Physics2D.queriesStartInColliders = original; }
    }

    [Test]
    public void MuzzleInsideEnemyHitsThatEnemyAndStopsAtItsBody()
    {
        var touching = Enemy(Vector2.zero, .4f);
        var behind = Enemy(new Vector2(2, 0));
        _spray.ApplyDamage(Vector2.zero, Vector2.right, 3);
        Assert.That(touching.CurrentHealth, Is.EqualTo(touching.MaxHealth - 3));
        Assert.That(behind.CurrentHealth, Is.EqualTo(behind.MaxHealth));
    }

    [Test]
    public void MatchPauseAndOtherMatchBodiesDoNotReceiveDamageOrBlockSameMatchTargets()
    {
        var clock = new Clock();
        FieldSimulationActor.Attach(_spray.gameObject, clock);
        var target = Enemy(new Vector2(2.5f, 0));
        FieldSimulationActor.Attach(target.gameObject, clock);
        var otherMatch = Enemy(new Vector2(1, 0), .4f);
        FieldSimulationActor.Attach(otherMatch.gameObject, new Clock());
        _spray.ApplyDamage(Vector2.zero, Vector2.right, 3);
        Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 3));
        Assert.That(otherMatch.CurrentHealth, Is.EqualTo(otherMatch.MaxHealth));
        clock.Paused = true;
        Assert.That(_spray.ApplyDamage(Vector2.zero, Vector2.right, 3), Is.False);
        Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 3));
    }

    [Test]
    public void DamageDoesNotInvokeAnAvailableSlowReceiver()
    {
        var target = Enemy(Vector2.right);
        var receiver = target.gameObject.AddComponent<SlowProbe>();
        _spray.ApplyDamage(Vector2.zero, Vector2.right, 3);
        Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 3));
        Assert.That(receiver.Calls, Is.Zero);
    }

    [TestCase(0f)] [TestCase(90f)] [TestCase(-135f)]
    public void BoundPresentationReusesEffectStopsAtEnemyAndClearsWhenDisabled(float angle)
    {
        var random = new PresentationRandom(); _spray.BindPresentationRandom(random);
        Vector3 origin = new Vector3(53, -71, 0);
        Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
        var target = Enemy((Vector2)origin + direction * 1.5f, .4f);
        Physics2D.SyncTransforms();
        _spray.Show(origin, direction);
        _spray.Show(origin, direction);
        Assert.That(_spray.IsEmitting, Is.True);
        Assert.That(random.Draws, Is.EqualTo(3), "Repeated held frames must reuse the effect and its seeds.");
        var systems = (ParticleSystem[])typeof(PlayerCryoSpray).GetField("_particles", Private).GetValue(_spray);
        foreach (var ps in systems) ps.Clear(false);
        var buffer = new[] { new ParticleSystem.Particle { position = new Vector3(3, 0, 0), startLifetime = 1, remainingLifetime = 1, startSize = .1f } };
        systems[0].SetParticles(buffer, 1);
        Call("AdvanceParticles", 0f);
        int count = systems[0].GetParticles(new ParticleSystem.Particle[256]);
        Assert.That(count, Is.Zero, "Cold past the front body's surface must not remain visible.");
        Object.DestroyImmediate(target.gameObject);
        Call("AdvanceParticles", .2f);
        Assert.That(Array.Exists(systems, ps => ps.particleCount > 0), Is.True);
        _spray.Stop();
        Call("AdvanceParticles", .6f);
        Assert.That(Array.TrueForAll(systems, ps => ps.particleCount == 0), Is.True, "Released spray fades without emitting new particles.");
        _spray.Show(origin, direction);
        _spray.enabled = false; Call("OnDisable");
        Assert.That(_spray.IsEmitting, Is.False);
        Assert.That(Array.TrueForAll(systems, ps => ps.particleCount == 0), Is.True);
    }

    private GameObject Create(string name, Vector2 position)
    {
        var item = new GameObject(name); SceneManager.MoveGameObjectToScene(item, _scene);
        item.transform.position = position; return item;
    }
    private EnemyHealth Enemy(Vector2 position, float radius = .1f, string layer = "EnemyHitBox")
    {
        var item = Create("Spray enemy", position); item.tag = "Enemy"; item.layer = LayerMask.NameToLayer(layer);
        var collider = item.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.radius = radius;
        var health = item.AddComponent<EnemyHealth>(); health.Initialize(); return health;
    }
    private void Call(string method, params object[] args) => typeof(PlayerCryoSpray).GetMethod(method, Private).Invoke(_spray, args);
    public sealed class SlowProbe : MonoBehaviour, ISlowEffectReceiver
    {
        public int Calls; public void ApplySlow(float multiplier, float duration) => Calls++;
    }
    private sealed class Clock : IMatchTime
    {
        private readonly Guid _epoch = Guid.NewGuid();
        public bool Paused;
        public bool IsAvailable => true;
        public double ElapsedSeconds => 0;
        public MatchTimeSnapshot Snapshot => new(new MatchId("spray-test"), _epoch, 0, 0, 0, Paused ? 0 : 1, Paused, true);
    }
    private sealed class PresentationRandom : IRandomStreams, IRandomSource
    {
        public int Draws;
        public bool IsAvailable => true;
        public IRandomSource GetStream(string id) { Assert.That(id, Is.EqualTo("vfx/player-cryo-spray")); return this; }
        public int NextInt(int min, int max) => 123 + Draws++;
        public uint NextUInt32() => throw new NotSupportedException();
        public float NextFloat() => throw new NotSupportedException();
        public float NextFloat(float min, float max) => throw new NotSupportedException();
        public double NextDouble() => throw new NotSupportedException();
        public double NextDouble(double min, double max) => throw new NotSupportedException();
    }
}

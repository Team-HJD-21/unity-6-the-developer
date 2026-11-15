using System;
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlayerHitProtectionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject _actor, _other;
    private PlayerInfo _health;
    private SpriteRenderer _body;
    private BoxCollider2D _feet, _inactiveFeet;
    private CircleCollider2D _sensor;
    private Color _original;
    private Clock _clock;
    private CancellationTokenSource _lifetime;
    private int EnemyMask => LayerMask.GetMask("Enemy", "EnemyFeetBox");

    [SetUp]
    public void SetUp()
    {
        _actor = new GameObject("Hit protection Player");
        _body = _actor.AddComponent<SpriteRenderer>(); _original = new Color(.8f, .6f, .4f, .9f); _body.color = _original;
        _feet = _actor.AddComponent<BoxCollider2D>();
        var inactive = new GameObject("Inactive facing"); inactive.transform.SetParent(_actor.transform);
        _inactiveFeet = inactive.AddComponent<BoxCollider2D>(); inactive.SetActive(false);
        _sensor = _actor.AddComponent<CircleCollider2D>(); _sensor.isTrigger = true;
        _health = _actor.AddComponent<PlayerInfo>(); Call("Awake");
        _clock = new Clock(); _lifetime = new CancellationTokenSource();
        FieldSimulationActor.Attach(_actor, _clock, _lifetime.Token, false);
        _actor.AddComponent<PlayerActorIdentity>().Bind(new PlayerId("hit-player"), 0);
    }

    [TearDown]
    public void TearDown()
    {
        if (_health != null) Call("OnDisable");
        if (_actor != null) Object.DestroyImmediate(_actor);
        if (_other != null) Object.DestroyImmediate(_other);
        _lifetime.Dispose();
    }

    private void Call(string method) => typeof(PlayerInfo).GetMethod(method, Private).Invoke(_health, null);
    private void Tick(double seconds) { _clock.Now = seconds; Call("FixedUpdate"); Call("LateUpdate"); }

    [Test]
    public void ActualHitProtectsImmediatelyIncludingReentrantDamageAndExpiresAtThreeSeconds()
    {
        int events = 0;
        _health.Damaged += _ => { events++; Assert.That(_health.ApplyDamage(7), Is.False); };
        Assert.That(_health.ApplyDamage(10), Is.True);
        Assert.That(_health.curHp, Is.EqualTo(390)); Assert.That(events, Is.EqualTo(1));
        Assert.That(_health.HitInvulnerabilityRemaining, Is.EqualTo(3));
        Tick(2.999); Assert.That(_health.ApplyDamage(10), Is.False); Assert.That(_health.curHp, Is.EqualTo(390));
        Tick(3); Assert.That(_health.IsHitInvulnerable, Is.False);
        Assert.That(_health.ApplyDamage(10), Is.True); Assert.That(_health.curHp, Is.EqualTo(380)); Assert.That(events, Is.EqualTo(2));
    }

    [Test]
    public void OnlyThisPlayersSolidCollidersIgnoreEnemiesAndRestoreOnlyTheAddedBits()
    {
        int original = LayerMask.GetMask("Enemy", "Water"), external = LayerMask.GetMask("WorldGeometry");
        _feet.excludeLayers = original;
        _other = new GameObject("Unaffected Player", typeof(BoxCollider2D));
        var other = _other.GetComponent<BoxCollider2D>(); int otherMask = other.excludeLayers;
        bool matrix = Physics2D.GetIgnoreLayerCollision(LayerMask.NameToLayer("PlayerFeetBox"), LayerMask.NameToLayer("EnemyFeetBox"));
        _health.ApplyDamage(1);
        Assert.That(_feet.excludeLayers.value & EnemyMask, Is.EqualTo(EnemyMask));
        _inactiveFeet.gameObject.SetActive(true);
        Assert.That(_inactiveFeet.excludeLayers.value & EnemyMask, Is.EqualTo(EnemyMask));
        Assert.That(_sensor.excludeLayers.value, Is.Zero); Assert.That(other.excludeLayers.value, Is.EqualTo(otherMask));
        _feet.excludeLayers = _feet.excludeLayers.value | external;
        Tick(3);
        Assert.That(_feet.excludeLayers.value, Is.EqualTo(original | external)); Assert.That(_inactiveFeet.excludeLayers.value, Is.Zero);
        Assert.That(Physics2D.GetIgnoreLayerCollision(LayerMask.NameToLayer("PlayerFeetBox"), LayerMask.NameToLayer("EnemyFeetBox")), Is.EqualTo(matrix));
    }

    [Test]
    public void GreyColorMultipliesWithoutAccumulationBlinksAndRestoresTheOriginalTint()
    {
        var material = _body.sharedMaterial;
        _health.ApplyDamage(1);
        Assert.That(_body.color.r, Is.EqualTo(_original.r * .55f).Within(.001));
        Assert.That(_body.color.g, Is.EqualTo(_original.g * .55f).Within(.001));
        var visible = _body.color; Call("LateUpdate"); Assert.That(_body.color, Is.EqualTo(visible));
        Tick(.13); Assert.That(_body.color.a, Is.EqualTo(_original.a * .18f).Within(.001));
        Tick(.25); Assert.That(_body.color.a, Is.EqualTo(_original.a).Within(.001));
        Tick(3); Assert.That(_body.color, Is.EqualTo(_original)); Assert.That(_body.sharedMaterial, Is.SameAs(material));
    }

    [Test]
    public void PauseFreezesProtectionAndRejectedHitsDoNotExtendItOrStartAutomaticRecovery()
    {
        Assert.That(_health.ApplyDamage(0), Is.False); Assert.That(_health.IsHitInvulnerable, Is.False);
        _clock.Paused = true; Assert.That(_health.ApplyDamage(1), Is.False); Assert.That(_health.IsHitInvulnerable, Is.False);
        _clock.Paused = false; _health.ApplyDamage(100); Tick(1);
        _clock.Paused = true; Call("FixedUpdate"); Assert.That(_health.HitInvulnerabilityRemaining, Is.EqualTo(2));
        _clock.Paused = false; Tick(2.9); Assert.That(_health.ApplyDamage(100), Is.False);
        Tick(5); Assert.That(_health.curHp, Is.EqualTo(308), "A rejected hit must not push back the original recovery pulse.");
    }

    [TestCase("disable")] [TestCase("death")] [TestCase("hub")] [TestCase("epoch")]
    [TestCase("cancel")] [TestCase("unavailable")] [TestCase("authority")]
    public void EndingTheProtectionScopeRestoresColorAndCollision(string ending)
    {
        _health.ApplyDamage(1);
        using var input = new InputLockService();
        switch (ending)
        {
            case "disable": _health.enabled = false; Call("OnDisable"); break;
            case "death": _health.Die(); break;
            case "hub": _actor.AddComponent<PlayerControlState>().BindHub(input); break;
            case "epoch": _clock.Epoch = Guid.NewGuid(); break;
            case "cancel": _lifetime.Cancel(); break;
            case "unavailable": _clock.Available = false; break;
            case "authority": typeof(PlayerInfo).GetField("_matchBound", Private).SetValue(_health, true); break;
        }
        Call("LateUpdate");
        Assert.That(_health.IsHitInvulnerable, Is.False); Assert.That(_feet.excludeLayers.value, Is.Zero);
        Assert.That(_inactiveFeet.excludeLayers.value, Is.Zero); Assert.That(_body.color, Is.EqualTo(_original));
    }

    private CheckpointCaptureContext Context() => new(new MatchConfig(new MatchId("hit-test"), MatchMode.Story,
        new StageId("stage"), new DefinitionId("normal"), "test", 1,
        new[] { new PlayerLoadoutSnapshot(new PlayerId("hit-player"), null) }), _clock.Now, _clock.Epoch);
    private static CheckpointRestoreContext Restore(CheckpointCaptureContext context, CheckpointSection section) => new(context,
        new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, context.Config.MatchId, context.Config.StageId,
            context.Config.DifficultyProfileId, context.Config.Mode, 1, "test", context.GameSeconds, 1, new[] { section }), CheckpointRestorePolicy.Default);

    [Test]
    public void CheckpointRestoresRemainingProtectionAndRollsBackWithoutReplayingDamage()
    {
        int events = 0; _health.Damaged += _ => events++;
        var writer = (ICheckpointParticipant)_health; var codec = new BinaryCheckpointPayloadCodec();
        _health.ApplyDamage(10); Tick(1); var saved = writer.Capture(Context(), codec); Assert.That(saved.Version, Is.EqualTo(4));
        Tick(3.5); _health.ApplyDamage(20);
        using (var step = writer.PrepareRestore(Restore(Context(), saved), saved, codec))
        {
            step.Apply(); Assert.That(_health.curHp, Is.EqualTo(390)); Assert.That(_health.HitInvulnerabilityRemaining, Is.EqualTo(2));
            Assert.That(_feet.excludeLayers.value & EnemyMask, Is.EqualTo(EnemyMask));
            Tick(5.49); Assert.That(_health.ApplyDamage(1), Is.False);
            step.Rollback(); Assert.That(_health.curHp, Is.EqualTo(370)); Assert.That(_health.HitInvulnerabilityRemaining, Is.EqualTo(3).Within(.001));
            Assert.That(events, Is.EqualTo(2));
        }
    }

    [Test]
    public void LegacyActorV3ClearsProtectionAndInvalidRemainingTimesAreRejectedBeforeMutation()
    {
        var writer = (ICheckpointParticipant)_health; var codec = new BinaryCheckpointPayloadCodec();
        _health.ApplyDamage(10); var saved = writer.Capture(Context(), codec);
        foreach (double value in new[] { double.NaN, -.1, 3.1 })
        {
            var bytes = saved.CopyPayload(); Buffer.BlockCopy(BitConverter.GetBytes(value), 0, bytes, bytes.Length - 8, 8);
            var invalid = codec.Write(writer.Key, 4, CheckpointSectionKind.World, w => w.Write(bytes));
            Assert.Throws<InvalidDataException>(() => writer.PrepareRestore(Restore(Context(), invalid), invalid, codec));
            Assert.That(_health.curHp, Is.EqualTo(390)); Assert.That(_health.HitInvulnerabilityRemaining, Is.EqualTo(3));
        }
        var old = codec.Write(writer.Key, 3, CheckpointSectionKind.World, w => w.Write(saved.CopyPayload(), 0, saved.PayloadLength - 8));
        using (var step = writer.PrepareRestore(Restore(Context(), old), old, codec)) step.Apply();
        Assert.That(_health.IsHitInvulnerable, Is.False); Assert.That(_body.color, Is.EqualTo(_original)); Assert.That(_feet.excludeLayers.value, Is.Zero);
    }

    private sealed class Clock : IMatchTime
    {
        public double Now; public bool Paused, Available = true; public Guid Epoch = Guid.NewGuid();
        public bool IsAvailable => Available; public double ElapsedSeconds => Now;
        public MatchTimeSnapshot Snapshot => new(new MatchId("hit-test"), Epoch, 0, Now, Now, Paused ? 0 : 1, Paused, true);
    }
}

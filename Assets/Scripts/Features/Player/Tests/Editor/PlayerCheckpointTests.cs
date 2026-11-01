// 실제 Player 원본의 저장 슬롯·체력·생존·배치와 UI 잠금 분리를 검증합니다.
// 무기·스킬·임시 생성물의 복원은 해당 writer/통합 검사와 구분합니다.
using System;
using System.IO;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlayerCheckpointTests
{
    private GameObject _actor;
    private PlayerInfo _health;
    private PlayerControlState _controls;
    private readonly BinaryCheckpointPayloadCodec _codec = new();
    private MatchConfig _config;
    private ICheckpointParticipant Writer => _health;
    private CheckpointCaptureContext Context => new(_config, 15, Guid.NewGuid());

    [SetUp]
    public void SetUp()
    {
        var id = new PlayerId("logical-player");
        _config = Config(id);
        _actor = new GameObject("Player checkpoint source"); _actor.SetActive(false);
        _actor.AddComponent<PlayerActorIdentity>().Bind(id, 0);
        _controls = _actor.AddComponent<PlayerControlState>();
        _health = _actor.AddComponent<PlayerInfo>();
        _actor.AddComponent<Rigidbody2D>().gravityScale = 0;
        _actor.SetActive(true);
        typeof(PlayerInfo).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_health, null);
    }
    [TearDown] public void TearDown() => Object.DestroyImmediate(_actor);
    private static MatchConfig Config(PlayerId player) => new(new MatchId("current"), MatchMode.Story,
        new StageId("stage"), new DefinitionId("normal"), "content", 1, new[] { new PlayerLoadoutSnapshot(player, null) });
    private CheckpointRestoreContext RestoreContext(CheckpointSection section)
    {
        var c = _config;
        var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, new MatchId("old-session"), c.StageId,
            c.DifficultyProfileId, c.Mode, c.RandomSeed, c.ContentVersion, 15, 1, new[] { section });
        return new CheckpointRestoreContext(Context, data, CheckpointRestorePolicy.Default);
    }
    private void Pose(Vector2 position, Vector2 velocity)
        => new CheckpointRigidbody2DState(position, 25, velocity, 0, true, false).Apply(_actor.GetComponent<Rigidbody2D>());

    [Test]
    public void ActualVitalsPoseAndLifeRestoreWithoutReplayingDeathOrUnlockingUi()
    {
        _health.TakeDamage(100); Pose(new Vector2(1, 2), new Vector2(3, 4));
        var saved = Writer.Capture(Context, _codec);
        Assert.That(saved.Key, Is.EqualTo("world/player/0/actor"));
        int deaths = 0; _health.onDeath.AddListener(() => deaths++);
        _controls.SetInputBlocked(true); _health.TakeDamage(400); Pose(new Vector2(7, 8), Vector2.zero);
        using var step = Writer.PrepareRestore(RestoreContext(saved), saved, _codec);
        Assert.That(_health.IsDead, Is.True, "Preparation must not revive the Player.");
        step.Apply();
        Assert.That(_health.curHp, Is.EqualTo(300)); Assert.That(_health.IsDead, Is.False);
        Assert.That((Vector2)_actor.transform.position, Is.EqualTo(new Vector2(1, 2)));
        Assert.That(_actor.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(new Vector2(3, 4)));
        Assert.That(_controls.CanAct, Is.False, "The current menu lock must survive world restoration.");
        _controls.SetInputBlocked(false); Assert.That(_controls.CanAct, Is.True);
        step.Rollback();
        Assert.That(_health.curHp, Is.Zero); Assert.That(_health.IsDead, Is.True);
        Assert.That((Vector2)_actor.transform.position, Is.EqualTo(new Vector2(7, 8)));
        Assert.That(_controls.CanAct, Is.False); Assert.That(deaths, Is.EqualTo(1));
    }

    [Test]
    public void OtherLogicalPlayerAndWrongSlotRejectBeforeChangingActualState()
    {
        var saved = Writer.Capture(Context, _codec);
        _config = Config(new PlayerId("other-player"));
        Assert.Throws<InvalidOperationException>(() => Writer.PrepareRestore(RestoreContext(saved), saved, _codec));
        _config = Config(new PlayerId("logical-player"));
        _actor.GetComponent<PlayerActorIdentity>().BindCheckpointSlot(1);
        Assert.Throws<InvalidOperationException>(() => Writer.Capture(Context, _codec));
        Assert.That(_health.curHp, Is.EqualTo(400));
    }

    [Test]
    public void FutureAndTruncatedPayloadRejectWithoutDamagingPlayer()
    {
        var saved = Writer.Capture(Context, _codec);
        var future = new CheckpointSection(saved.Key, 2, saved.Kind, saved.CopyPayload());
        Assert.Throws<NotSupportedException>(() => Writer.PrepareRestore(RestoreContext(future), future, _codec));
        var truncated = new CheckpointSection(saved.Key, 1, saved.Kind, new byte[3]);
        Assert.Throws<InvalidDataException>(() => Writer.PrepareRestore(RestoreContext(truncated), truncated, _codec));
        Assert.That(_health.curHp, Is.EqualTo(400)); Assert.That(_health.IsDead, Is.False);
    }

    [Test]
    public void UnassignedSlotHasNoPersistentKeyAndRecoveryCannotStackOrRunAfterDeath()
    {
        var fixture = new GameObject("Unassigned actor");
        try
        {
            var identity = fixture.AddComponent<PlayerActorIdentity>(); identity.Bind(new PlayerId("unassigned"));
            Assert.Throws<InvalidOperationException>(() => _ = identity.CheckpointKeyPrefix);
            Assert.Throws<ArgumentOutOfRangeException>(() => identity.BindCheckpointSlot(-1));
        }
        finally { Object.DestroyImmediate(fixture); }
        _health.TakeDamage(10); _health.RecoverHp();
        int once = _health.curHp;
        _health.RecoverHp(); Assert.That(_health.curHp, Is.EqualTo(once));
        _health.TakeDamage(400); _health.RecoverHp();
        Assert.That(_health.curHp, Is.Zero); Assert.That(_health.IsRecovering, Is.False);
    }
}

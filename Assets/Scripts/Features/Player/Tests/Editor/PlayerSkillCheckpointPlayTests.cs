// 실제 Player prefab의 폭탄·장벽·비콘·임시 포탑을 복원하고 피해·수량·논리 ID를 확인합니다.
// 각 Feature writer를 검사하며 전체 Scene의 CheckpointService/재실행 인수를 대신하지 않습니다.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed class PlayerSkillCheckpointPlayTests
{
    private sealed class Timestamp : ITimestampSource
    { public bool TryReadSeconds(out double seconds) { seconds = Time.unscaledTimeAsDouble; return true; } }
    private SceneSetup[] _setup;
    private MatchClock _clock;
    private MatchPresenceService _presence;
    private MatchConfig _config;
    private GameObject _actor;
    private PlayerSkillController _skills;
    private Tile _groundTile;
    private readonly BinaryCheckpointPayloadCodec _codec = new();
    [SetUp] public void RememberScenes()
    { if (!UnityEngine.Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
    [UnitySetUp] public IEnumerator CreatePlayer()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var player = new PlayerId("skill-owner"); var match = new MatchId("skill-checkpoint");
        _config = new MatchConfig(match, MatchMode.Story, new StageId("stage"), new DefinitionId("normal"), "content", 1,
            new[] { new PlayerLoadoutSnapshot(player, null) });
        _clock = new MatchClock(match, new Timestamp()); _presence = new MatchPresenceService(_config, () => true, _clock.SetPaused);
        _actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), new Vector3(10000, 10000, 0), Quaternion.identity);
        var identity = _actor.GetComponent<PlayerActorIdentity>() ?? _actor.AddComponent<PlayerActorIdentity>(); identity.Bind(player, 0);
        _actor.GetComponent<PlayerInputHandler>().enabled = false;
        var presence = _actor.GetComponent<PlayerMatchPresence>() ?? _actor.AddComponent<PlayerMatchPresence>(); presence.Bind(_presence.RegisterActor(player), _clock, default);
        _skills = _actor.GetComponent<PlayerSkillController>();
        yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        _presence?.Dispose(); _clock?.Dispose();
        if (_groundTile != null) Object.DestroyImmediate(_groundTile);
        if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        if (_setup != null && _setup.Any(s => s.isLoaded) && _setup.Count(s => s.isActive) == 1)
            EditorSceneManager.RestoreSceneManagerSetup(_setup);
    }
    private CheckpointCaptureContext Context => new(_config, _clock.ElapsedSeconds, _clock.Snapshot.Epoch);
    private CheckpointSection Capture(ICheckpointParticipant writer) { _clock.Pump(); _clock.SetPaused(true); return writer.Capture(Context, _codec); }
    private ICheckpointRestoreStep Prepare(ICheckpointParticipant writer, CheckpointSection section)
    {
        var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, _config.MatchId, _config.StageId, _config.DifficultyProfileId,
            _config.Mode, _config.RandomSeed, _config.ContentVersion, _clock.ElapsedSeconds, 1, new[] { section });
        return writer.PrepareRestore(new CheckpointRestoreContext(Context, data, CheckpointRestorePolicy.Default), section, _codec);
    }
    private IEnumerator WaitGame(double duration)
    {
        _clock.SetPaused(false); _clock.Pump(); double target = _clock.ElapsedSeconds + duration, timeout = Time.realtimeSinceStartupAsDouble + duration + 4;
        while (_clock.ElapsedSeconds < target && Time.realtimeSinceStartupAsDouble < timeout) { _clock.Pump(); yield return null; }
        Assert.That(_clock.ElapsedSeconds, Is.GreaterThanOrEqualTo(target));
    }
    private EnemyHealth Enemy(Vector3 position, int health = 1000)
    {
        var actor = new GameObject("Skill checkpoint target"); actor.tag = "Enemy"; actor.layer = LayerMask.NameToLayer("Enemy"); actor.transform.position = position;
        var collider = actor.AddComponent<CircleCollider2D>(); collider.radius = .25f; collider.isTrigger = true;
        var target = actor.AddComponent<EnemyHealth>(); var data = new SerializedObject(target);
        data.FindProperty("maxHealth").intValue = health; data.ApplyModifiedPropertiesWithoutUndo(); target.Initialize(); Physics2D.SyncTransforms(); return target;
    }
    private T[] OwnedScene<T>() where T : Component => Object.FindObjectsByType<T>(FindObjectsSortMode.None).Where(o => o.gameObject.scene == _actor.scene).ToArray();
    private void MovePlayer(Vector3 position)
    { _actor.transform.position = position; var body = _actor.GetComponent<Rigidbody2D>(); body.position = position; Physics2D.SyncTransforms(); }

    [UnityTest]
    public IEnumerator TimeBombKeepsIdentityAndFuseThenAppliesOneExplosionAfterRestore()
    {
        var bomb = _actor.GetComponent<PlayerBomb>(); _skills.Loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.None);
        var enemy = Enemy(_actor.transform.position + Vector3.right * 2);
        Scene hub = SceneManager.CreateScene("Skill Checkpoint Hub"); SceneManager.SetActiveScene(hub);
        Assert.That(bomb.TryUseSkill(), Is.True); int charges = bomb.CurrentBombCount;
        var original = OwnedScene<PlayerBombExplosion>().Single(); var id = original.GetComponent<SceneEntityIdentity>().EntityId;
        var section = Capture(bomb);
        _clock.SetPaused(false); Assert.That(original.Explode(), Is.True); Assert.That(enemy.CurrentHealth, Is.EqualTo(750));
        _clock.SetPaused(true); using var restore = Prepare(bomb, section); restore.Apply();
        var recreated = OwnedScene<PlayerBombExplosion>().Single(b => b.isActiveAndEnabled);
        Assert.That(recreated.GetComponent<SceneEntityIdentity>().EntityId, Is.EqualTo(id));
        Assert.That(bomb.CurrentBombCount, Is.EqualTo(charges));
        enemy.Initialize(); // Enemy HP is owned by a different writer; isolate the restored bomb's future damage.
        yield return WaitGame(2.3);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(750));
    }

    [UnityTest]
    public IEnumerator AlreadyExplodedBombDoesNotReplayAreaDamage()
    {
        var bomb = _actor.GetComponent<PlayerBomb>(); _skills.Loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.None);
        var enemy = Enemy(_actor.transform.position + Vector3.right);
        Assert.That(bomb.TryUseSkill(), Is.True); Assert.That(OwnedScene<PlayerBombExplosion>().Single().Explode(), Is.True);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(750)); var section = Capture(bomb);
        using var restore = Prepare(bomb, section); restore.Apply();
        yield return WaitGame(.15); Assert.That(enemy.CurrentHealth, Is.EqualTo(750));
    }

    [UnityTest]
    public IEnumerator BarrierAndBeaconRestoreHealthIdentityChargesAndPendingFirstPulse()
    {
        var barrier = _actor.GetComponent<PlayerBarrierSkill>(); var beacon = _actor.GetComponent<PlayerBeaconSkill>();
        _skills.Loadout.Restore(PlayerSkillId.EnergyBarrier, PlayerSkillId.LureBeacon);
        Assert.That(barrier.TryUseSkill(), Is.True); MovePlayer(_actor.transform.position + Vector3.right * 2);
        Assert.That(barrier.TryUseSkill(), Is.True); Assert.That(beacon.TryUseSkill(), Is.True);
        var wall = OwnedScene<PlayerFieldDevice>().Single(d => d.Kind == PlayerFieldDeviceKind.Barrier);
        var lure = OwnedScene<PlayerFieldDevice>().Single(d => d.Kind == PlayerFieldDeviceKind.Beacon);
        wall.TakeDamage(50); lure.TakeDamage(10); var wallId = wall.EntityId; var lureId = lure.EntityId; int charges = barrier.Charges;
        var wallData = Capture(barrier); var lureData = Capture(beacon);
        wall.TakeDamage(1000); lure.TakeDamage(1000);
        using var wallStep = Prepare(barrier, wallData); using var lureStep = Prepare(beacon, lureData);
        wallStep.Apply(); lureStep.Apply();
        var active = OwnedScene<PlayerFieldDevice>().Where(d => d.IsAlive).ToArray(); Assert.That(active.Length, Is.EqualTo(2));
        Assert.That(active.Single(d => d.EntityId == wallId).Health, Is.EqualTo(250));
        Assert.That(active.Single(d => d.EntityId == lureId).Health, Is.EqualTo(70)); Assert.That(barrier.Charges, Is.EqualTo(charges));
        _clock.SetPaused(false); Assert.That(barrier.TryUseSkill(), Is.True); Assert.That(barrier.IsPending, Is.True);
        var pending = Capture(barrier); barrier.CancelPlacement(); using var pendingStep = Prepare(barrier, pending); pendingStep.Apply();
        Assert.That(barrier.IsPending, Is.True); _clock.SetPaused(false); barrier.Tick(16); Assert.That(barrier.IsPending, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TemporaryTurretRestoresOwnedShotsAndInventoryWithoutDoubleReturningCharges()
    {
        var skill = _actor.GetComponent<PlayerTurretSkill>(); _skills.Loadout.Restore(PlayerSkillId.TemporaryTurret, PlayerSkillId.None);
        var grid = new GameObject("Checkpoint placement ground", typeof(Grid)); grid.transform.position = _actor.transform.position;
        var mapObject = new GameObject("Ground", typeof(Tilemap), typeof(TilemapCollider2D)); mapObject.transform.SetParent(grid.transform, false);
        var map = mapObject.GetComponent<Tilemap>(); _groundTile = ScriptableObject.CreateInstance<Tile>(); _groundTile.colliderType = Tile.ColliderType.None;
        for (int y = -10; y <= 10; y++) for (int x = -10; x <= 10; x++) map.SetTile(new Vector3Int(x, y, 0), _groundTile);
        var skillData = new SerializedObject(skill); skillData.FindProperty("_placementGround").objectReferenceValue = map; skillData.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(skill.TryUseSkill(), Is.True); Assert.That(skill.TryConfirmPlacement(_actor.transform.position + Vector3.right * 10), Is.True);
        var turret = OwnedScene<TemporaryTurret>().Single(); turret.Health.TakeDamage(50); var id = turret.Health.EntityId;
        var enemy = Enemy(turret.transform.position + Vector3.right * 18, 100);
        double timeout = Time.realtimeSinceStartupAsDouble + 3;
        while (OwnedScene<TemporaryTurretProjectile>().Length == 0 && Time.realtimeSinceStartupAsDouble < timeout) { _clock.Pump(); yield return null; }
        Assert.That(OwnedScene<TemporaryTurretProjectile>().Length, Is.GreaterThan(0));
        var section = Capture(skill); int charges = skill.CurrentTurretCount;
        turret.Health.TakeDamage(1000); _clock.SetPaused(false); yield return WaitGame(.4); _clock.SetPaused(true);
        Assert.That(skill.RepairRemaining, Is.GreaterThan(0));
        using var restore = Prepare(skill, section); restore.Apply();
        var recreated = OwnedScene<TemporaryTurret>().Single(t => t.Health.IsAlive);
        Assert.That(recreated.Health.EntityId, Is.EqualTo(id)); Assert.That(recreated.Health.Health, Is.EqualTo(50));
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(charges)); Assert.That(skill.RepairRemaining, Is.Zero);
        Assert.That(OwnedScene<TemporaryTurretProjectile>().Any(p => p.isActiveAndEnabled), Is.True);
        enemy.Initialize(); _clock.SetPaused(false); timeout = Time.realtimeSinceStartupAsDouble + 1;
        while (enemy.CurrentHealth == 100 && Time.realtimeSinceStartupAsDouble < timeout) { _clock.Pump(); yield return null; }
        Assert.That(enemy.CurrentHealth, Is.EqualTo(94));
    }

    [UnityTest]
    public IEnumerator LoadoutRestoresSelectionWithoutUnlockingUiAndRejectsUnknownSkills()
    {
        _skills.Loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.LureBeacon);
        var writer = (ICheckpointParticipant)_skills; var section = Capture(writer);
        _skills.Loadout.Restore(PlayerSkillId.EnergyBarrier, PlayerSkillId.None);
        var controls = _actor.GetComponent<PlayerControlState>(); controls.SetInputBlocked(true);
        using var restore = Prepare(writer, section); restore.Apply();
        Assert.That(_skills.Loadout.Q, Is.EqualTo(PlayerSkillId.Bomb)); Assert.That(_skills.Loadout.E, Is.EqualTo(PlayerSkillId.LureBeacon));
        Assert.That(controls.IsBlocked, Is.True);
        var bad = _codec.Write(section.Key, 1, section.Kind, output =>
        { _codec.WriteText(output, _actor.GetComponent<PlayerActorIdentity>().PlayerId.Value); output.Write(99); output.Write(0); });
        Assert.Throws<InvalidDataException>(() => Prepare(writer, bad));
        Assert.That(_skills.Loadout.Q, Is.EqualTo(PlayerSkillId.Bomb));
        yield return null;
    }

    [UnityTest]
    public IEnumerator DamageCallbacksCannotCaptureAHalfCompletedBombExplosion()
    {
        var bomb = _actor.GetComponent<PlayerBomb>(); _skills.Loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.None);
        var first = Enemy(_actor.transform.position + Vector3.right, 250); var second = Enemy(_actor.transform.position + Vector3.left, 250);
        Assert.That(bomb.TryUseSkill(), Is.True); int rejected = 0;
        Action<EnemyHealth> tryCapture = _ =>
        {
            try { ((ICheckpointParticipant)bomb).Capture(Context, _codec); }
            catch (InvalidOperationException) { rejected++; }
        };
        first.Died += tryCapture; second.Died += tryCapture;
        Assert.That(OwnedScene<PlayerBombExplosion>().Single().Explode(), Is.True);
        Assert.That(rejected, Is.EqualTo(2)); Assert.That(first.CurrentHealth, Is.Zero); Assert.That(second.CurrentHealth, Is.Zero);
        Assert.That(Capture(bomb).PayloadLength, Is.GreaterThan(0));
        yield return null;
    }
}
